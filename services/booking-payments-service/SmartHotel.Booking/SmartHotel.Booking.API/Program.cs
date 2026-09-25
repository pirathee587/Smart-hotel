using System.Net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SmartHotel.Booking.API.Services;
using SmartHotel.Booking.Application;
using SmartHotel.Booking.Infrastructure;
using SmartHotel.Booking.Infrastructure.Persistence;
using SmartHotel.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Configure dual-port Kestrel: HTTP/1.1 for REST, HTTP/2 cleartext (h2c) for gRPC
builder.WebHost.ConfigureKestrel(options =>
{
    var restPort = builder.Configuration.GetValue<int?>("BOOKING_HTTP_PORT") ?? builder.Configuration.GetValue<int?>("PORT") ?? 5003;
    var grpcPort = builder.Configuration.GetValue<int?>("BOOKING_GRPC_PORT") ?? builder.Configuration.GetValue<int?>("GRPC_PORT") ?? 5013;

    options.Listen(IPAddress.Any, restPort, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1;
    });

    options.Listen(IPAddress.Any, grpcPort, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
});

// 1. Add services to container
builder.Services.AddControllers();
builder.Services.AddGrpc();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();
builder.Services.AddHttpClient<IAttendanceSummaryVerifier, AttendanceSummaryVerifier>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:FieldOpsUrl"] ?? "http://field-ops-service:8084/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

// 2. Swagger with Bearer token authentication
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SmartHotel Booking & Payments Service API",
        Version = "v1",
        Description = "Reservations, PayHere Payments, Concurrency Protection, Kiosk, Reviews & Complaints Service (RS256 JWT, CQRS/MediatR)"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Enter 'Bearer' [space] and then your valid RS256 token in the text input below.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header
            },
            new List<string>()
        }
    });
});

// 3. Clean Architecture layers
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddApplication();

// 4. RS256 JWT Authentication configuration with resilient JWKS Key Resolver
var issuer = builder.Configuration["Jwt:Issuer"] ?? builder.Configuration["Authentication:Issuer"] ?? "SmartHotel.Identity";
var audience = builder.Configuration["Jwt:Audience"] ?? builder.Configuration["Authentication:Audience"] ?? "SmartHotel.Clients";

builder.Services.AddHttpClient<IBookingJwksResolver, BookingJwksResolver>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IBookingJwksResolver>((options, keyResolver) =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
            {
                return keyResolver.GetSigningKeysAsync(kid).GetAwaiter().GetResult();
            }
        };
    });

builder.Services.AddHotelDepartmentAuthorization();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SmartHotel Booking API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGrpcService<BookingGrpcService>();
app.MapHealthChecks("/health");

// Database initialization & Startup seeding
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        if (context.Database.IsRelational())
        {
            await context.Database.MigrateAsync();
            await context.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "FinanceApprovalSettings" (
                  "Id" uuid PRIMARY KEY, "ManagerExpenseApprovalLimit" numeric(18,2) NOT NULL,
                  "ManagerRefundApprovalLimit" numeric(18,2) NOT NULL, "Currency" varchar(3) NOT NULL,
                  "UpdatedByUserId" uuid NOT NULL, "CreatedAtUtc" timestamptz NOT NULL, "UpdatedAtUtc" timestamptz NULL,
                  CONSTRAINT "CK_FinanceApprovalSettings_Limits" CHECK ("ManagerExpenseApprovalLimit" >= 0 AND "ManagerRefundApprovalLimit" >= 0),
                  CONSTRAINT "CK_FinanceApprovalSettings_Currency" CHECK (char_length("Currency") = 3)
                );
                CREATE TABLE IF NOT EXISTS "ExpenseRequests" (
                  "Id" uuid PRIMARY KEY, "Amount" numeric(18,2) NOT NULL, "Currency" varchar(3) NOT NULL,
                  "Description" varchar(1000) NOT NULL, "Status" varchar(20) NOT NULL,
                  "ApprovalStage" varchar(20) NOT NULL, "ExecutionStatus" varchar(20) NOT NULL,
                  "SubmittedByUserId" uuid NOT NULL, "SubmittedByDepartmentId" uuid NOT NULL,
                  "IdempotencyKey" varchar(100) NOT NULL, "DecidedByUserId" uuid NULL,
                  "DecidedAtUtc" timestamptz NULL, "DecisionReason" varchar(1000) NULL,
                  "Category" varchar(100) NOT NULL, "VendorReference" varchar(200) NULL,
                  "CreatedAtUtc" timestamptz NOT NULL, "UpdatedAtUtc" timestamptz NULL,
                  CONSTRAINT "CK_ExpenseRequests_Amount" CHECK ("Amount" > 0),
                  CONSTRAINT "CK_ExpenseRequests_Currency" CHECK (char_length("Currency") = 3)
                );
                CREATE TABLE IF NOT EXISTS "RefundRequests" (
                  "Id" uuid PRIMARY KEY, "PaymentId" uuid NOT NULL REFERENCES "Payments"("Id") ON DELETE RESTRICT,
                  "Amount" numeric(18,2) NOT NULL, "Currency" varchar(3) NOT NULL,
                  "Description" varchar(1000) NOT NULL, "Status" varchar(20) NOT NULL,
                  "ApprovalStage" varchar(20) NOT NULL, "ExecutionStatus" varchar(20) NOT NULL,
                  "SubmittedByUserId" uuid NOT NULL, "SubmittedByDepartmentId" uuid NOT NULL,
                  "IdempotencyKey" varchar(100) NOT NULL, "DecidedByUserId" uuid NULL,
                  "DecidedAtUtc" timestamptz NULL, "DecisionReason" varchar(1000) NULL,
                  "CreatedAtUtc" timestamptz NOT NULL, "UpdatedAtUtc" timestamptz NULL,
                  CONSTRAINT "CK_RefundRequests_Amount" CHECK ("Amount" > 0),
                  CONSTRAINT "CK_RefundRequests_Currency" CHECK (char_length("Currency") = 3)
                );
                CREATE TABLE IF NOT EXISTS "FinanceAccessGrants" (
                  "Id" uuid PRIMARY KEY, "UserId" uuid NOT NULL, "Permission" varchar(100) NOT NULL,
                  "GrantedByUserId" uuid NOT NULL, "IsActive" boolean NOT NULL,
                  "CreatedAtUtc" timestamptz NOT NULL, "UpdatedAtUtc" timestamptz NULL
                );
                CREATE TABLE IF NOT EXISTS "FinanceAuditLogs" (
                  "Id" uuid PRIMARY KEY, "ActorUserId" uuid NOT NULL, "Action" varchar(100) NOT NULL,
                  "EntityType" varchar(100) NOT NULL, "EntityId" uuid NOT NULL, "Details" varchar(2000) NOT NULL,
                  "CreatedAtUtc" timestamptz NOT NULL, "UpdatedAtUtc" timestamptz NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_ExpenseRequests_IdempotencyKey" ON "ExpenseRequests"("IdempotencyKey");
                CREATE INDEX IF NOT EXISTS "IX_ExpenseRequests_Workflow" ON "ExpenseRequests"("Status", "ApprovalStage", "CreatedAtUtc");
                CREATE INDEX IF NOT EXISTS "IX_ExpenseRequests_Department" ON "ExpenseRequests"("SubmittedByDepartmentId");
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_RefundRequests_IdempotencyKey" ON "RefundRequests"("IdempotencyKey");
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_RefundRequests_PaymentId" ON "RefundRequests"("PaymentId");
                CREATE INDEX IF NOT EXISTS "IX_RefundRequests_Workflow" ON "RefundRequests"("Status", "ApprovalStage", "CreatedAtUtc");
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_FinanceAccessGrants_UserPermission" ON "FinanceAccessGrants"("UserId", "Permission");
                CREATE INDEX IF NOT EXISTS "IX_FinanceAccessGrants_Active" ON "FinanceAccessGrants"("UserId", "IsActive");
                CREATE INDEX IF NOT EXISTS "IX_FinanceAuditLogs_Entity" ON "FinanceAuditLogs"("EntityType", "EntityId", "CreatedAtUtc");
                CREATE INDEX IF NOT EXISTS "IX_FinanceAuditLogs_Actor" ON "FinanceAuditLogs"("ActorUserId", "CreatedAtUtc");
                """);
            await context.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "Payments" ADD COLUMN IF NOT EXISTS "ProviderPaymentReference" varchar(200) NULL;
                ALTER TABLE "Payments" ADD COLUMN IF NOT EXISTS "ProviderCheckoutReference" varchar(200) NULL;
                ALTER TABLE "Payments" ADD COLUMN IF NOT EXISTS "RefundedAmount" numeric(18,2) NOT NULL DEFAULT 0;
                UPDATE "Payments" SET "RefundedAmount" = COALESCE("RefundAmount", 0) WHERE "RefundedAmount" = 0 AND "RefundAmount" IS NOT NULL;
                ALTER TABLE "Payments" ADD COLUMN IF NOT EXISTS "FundsHeldAtUtc" timestamptz NULL;
                ALTER TABLE "Payments" ADD COLUMN IF NOT EXISTS "ReleasedAtUtc" timestamptz NULL;
                ALTER TABLE "Payments" ADD COLUMN IF NOT EXISTS "SettledAtUtc" timestamptz NULL;
                ALTER TABLE "FinanceApprovalSettings" ADD COLUMN IF NOT EXISTS "ManagerReleaseApprovalLimit" numeric(18,2) NOT NULL DEFAULT 0;
                DROP INDEX IF EXISTS "IX_RefundRequests_PaymentId";
                CREATE INDEX IF NOT EXISTS "IX_RefundRequests_PaymentId" ON "RefundRequests"("PaymentId");
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_Payments_ProviderReference" ON "Payments"("Provider", "ProviderPaymentReference") WHERE "ProviderPaymentReference" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS "IX_Payments_Status" ON "Payments"("Status");
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_Payments_ProviderOrder" ON "Payments"("Provider", "PayHereOrderId");
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_Payments_ProviderPaymentId" ON "Payments"("PayHerePaymentId") WHERE "PayHerePaymentId" IS NOT NULL;

                CREATE TABLE IF NOT EXISTS "EscrowReleaseRequests" (
                  "Id" uuid PRIMARY KEY, "PaymentId" uuid NOT NULL REFERENCES "Payments"("Id") ON DELETE RESTRICT,
                  "Amount" numeric(18,2) NOT NULL, "Currency" varchar(3) NOT NULL, "Description" varchar(1000) NOT NULL,
                  "Status" varchar(20) NOT NULL, "ApprovalStage" varchar(20) NOT NULL, "ExecutionStatus" varchar(20) NOT NULL,
                  "SubmittedByUserId" uuid NOT NULL, "SubmittedByDepartmentId" uuid NOT NULL, "IdempotencyKey" varchar(100) NOT NULL,
                  "DecidedByUserId" uuid NULL, "DecidedAtUtc" timestamptz NULL, "DecisionReason" varchar(1000) NULL,
                  "ProviderReleaseReference" varchar(200) NULL, "CreatedAtUtc" timestamptz NOT NULL, "UpdatedAtUtc" timestamptz NULL,
                  CONSTRAINT "CK_EscrowReleaseRequests_Amount" CHECK ("Amount" > 0),
                  CONSTRAINT "CK_EscrowReleaseRequests_Currency" CHECK (char_length("Currency") = 3)
                );
                CREATE TABLE IF NOT EXISTS "EscrowLifecycleRecords" (
                  "Id" uuid PRIMARY KEY, "PaymentId" uuid NOT NULL REFERENCES "Payments"("Id") ON DELETE CASCADE,
                  "EventType" varchar(30) NOT NULL, "ProviderReference" varchar(200) NOT NULL, "IdempotencyKey" varchar(150) NOT NULL,
                  "Details" varchar(2000) NOT NULL, "CreatedAtUtc" timestamptz NOT NULL, "UpdatedAtUtc" timestamptz NULL
                );
                CREATE TABLE IF NOT EXISTS "PaymentApprovalHistory" (
                  "Id" uuid PRIMARY KEY, "RequestId" uuid NOT NULL, "RequestType" varchar(30) NOT NULL, "ActorUserId" uuid NOT NULL,
                  "Stage" varchar(20) NOT NULL, "Action" varchar(20) NOT NULL, "Reason" varchar(1000) NULL,
                  "CreatedAtUtc" timestamptz NOT NULL, "UpdatedAtUtc" timestamptz NULL
                );
                CREATE TABLE IF NOT EXISTS "SettlementRecords" (
                  "Id" uuid PRIMARY KEY, "PaymentId" uuid NOT NULL REFERENCES "Payments"("Id") ON DELETE RESTRICT,
                  "Amount" numeric(18,2) NOT NULL, "Currency" varchar(3) NOT NULL, "Status" varchar(20) NOT NULL,
                  "ProviderSettlementReference" varchar(200) NOT NULL, "ConfirmedAtUtc" timestamptz NULL,
                  "CreatedAtUtc" timestamptz NOT NULL, "UpdatedAtUtc" timestamptz NULL,
                  CONSTRAINT "CK_SettlementRecords_Amount" CHECK ("Amount" > 0),
                  CONSTRAINT "CK_SettlementRecords_Currency" CHECK (char_length("Currency") = 3)
                );
                CREATE TABLE IF NOT EXISTS "PaymentWebhookReceipts" (
                  "Id" uuid PRIMARY KEY, "Provider" integer NOT NULL, "ProviderEventId" varchar(200) NOT NULL,
                  "EventType" varchar(50) NOT NULL, "PayloadHash" varchar(64) NOT NULL, "ProcessedAtUtc" timestamptz NOT NULL,
                  "CreatedAtUtc" timestamptz NOT NULL, "UpdatedAtUtc" timestamptz NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_EscrowReleaseRequests_IdempotencyKey" ON "EscrowReleaseRequests"("IdempotencyKey");
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_EscrowReleaseRequests_PaymentId" ON "EscrowReleaseRequests"("PaymentId");
                CREATE INDEX IF NOT EXISTS "IX_EscrowReleaseRequests_Workflow" ON "EscrowReleaseRequests"("Status", "ApprovalStage", "CreatedAtUtc");
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_EscrowLifecycleRecords_IdempotencyKey" ON "EscrowLifecycleRecords"("IdempotencyKey");
                CREATE INDEX IF NOT EXISTS "IX_EscrowLifecycleRecords_Payment" ON "EscrowLifecycleRecords"("PaymentId", "CreatedAtUtc");
                CREATE INDEX IF NOT EXISTS "IX_PaymentApprovalHistory_Request" ON "PaymentApprovalHistory"("RequestId", "CreatedAtUtc");
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_SettlementRecords_ProviderReference" ON "SettlementRecords"("ProviderSettlementReference");
                CREATE INDEX IF NOT EXISTS "IX_SettlementRecords_Status" ON "SettlementRecords"("Status", "CreatedAtUtc");
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_PaymentWebhookReceipts_Event" ON "PaymentWebhookReceipts"("Provider", "ProviderEventId");
                """);
            await context.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "ExpenseRequests" ADD COLUMN IF NOT EXISTS "ReceiptUrl" varchar(1000) NULL;
                ALTER TABLE "ExpenseRequests" ADD COLUMN IF NOT EXISTS "VerificationStatus" varchar(20) NOT NULL DEFAULT 'Pending';
                ALTER TABLE "ExpenseRequests" ADD COLUMN IF NOT EXISTS "VerifiedByUserId" uuid NULL;
                ALTER TABLE "ExpenseRequests" ADD COLUMN IF NOT EXISTS "VerifiedAtUtc" timestamptz NULL;

                CREATE TABLE IF NOT EXISTS "FinanceInvoices" (
                  "Id" uuid PRIMARY KEY, "InvoiceNumber" varchar(40) NOT NULL, "Type" varchar(20) NOT NULL,
                  "Status" varchar(20) NOT NULL, "BookingId" uuid NULL, "PaymentId" uuid NULL,
                  "ChargeReference" varchar(150) NOT NULL, "PartyName" varchar(200) NOT NULL, "PartyEmail" varchar(254) NOT NULL,
                  "Subtotal" numeric(18,2) NOT NULL, "TaxAmount" numeric(18,2) NOT NULL, "TotalAmount" numeric(18,2) NOT NULL,
                  "PaidAmount" numeric(18,2) NOT NULL, "Currency" varchar(3) NOT NULL, "IssueDate" date NOT NULL, "DueDate" date NOT NULL,
                  "CreatedByUserId" uuid NOT NULL, "ValidatedByUserId" uuid NULL, "IssuedAtUtc" timestamptz NULL,
                  "CreatedAtUtc" timestamptz NOT NULL, "UpdatedAtUtc" timestamptz NULL,
                  CONSTRAINT "CK_FinanceInvoices_Amounts" CHECK ("Subtotal" >= 0 AND "TaxAmount" >= 0 AND "TotalAmount" >= 0 AND "PaidAmount" >= 0 AND "PaidAmount" <= "TotalAmount"),
                  CONSTRAINT "CK_FinanceInvoices_Currency" CHECK (char_length("Currency") = 3)
                );
                CREATE TABLE IF NOT EXISTS "FinanceCreditNotes" (
                  "Id" uuid PRIMARY KEY, "InvoiceId" uuid NOT NULL REFERENCES "FinanceInvoices"("Id") ON DELETE RESTRICT,
                  "CreditNoteNumber" varchar(40) NOT NULL, "Amount" numeric(18,2) NOT NULL, "Currency" varchar(3) NOT NULL,
                  "Reason" varchar(1000) NOT NULL, "Status" varchar(20) NOT NULL, "CreatedByUserId" uuid NOT NULL,
                  "AuthorizedByUserId" uuid NULL, "CreatedAtUtc" timestamptz NOT NULL, "UpdatedAtUtc" timestamptz NULL,
                  CONSTRAINT "CK_FinanceCreditNotes_Amount" CHECK ("Amount" > 0),
                  CONSTRAINT "CK_FinanceCreditNotes_Currency" CHECK (char_length("Currency") = 3)
                );
                CREATE TABLE IF NOT EXISTS "FinancePayrollRecords" (
                  "Id" uuid PRIMARY KEY, "SourcePayrollId" uuid NOT NULL, "EmployeeId" uuid NOT NULL,
                  "PeriodStart" date NOT NULL, "PeriodEnd" date NOT NULL, "BaseSalary" numeric(18,2) NOT NULL,
                  "Allowances" numeric(18,2) NOT NULL, "Deductions" numeric(18,2) NOT NULL, "NetSalary" numeric(18,2) NOT NULL,
                  "Currency" varchar(3) NOT NULL, "Status" varchar(30) NOT NULL, "SubmittedByUserId" uuid NOT NULL,
                  "VerifiedByUserId" uuid NULL, "ManagerApprovedByUserId" uuid NULL, "OwnerApprovedByUserId" uuid NULL,
                  "DisbursedAtUtc" timestamptz NULL, "DisbursementReference" varchar(200) NULL,
                  "CreatedAtUtc" timestamptz NOT NULL, "UpdatedAtUtc" timestamptz NULL,
                  CONSTRAINT "CK_FinancePayrollRecords_Amounts" CHECK ("BaseSalary" >= 0 AND "Allowances" >= 0 AND "Deductions" >= 0 AND "NetSalary" >= 0),
                  CONSTRAINT "CK_FinancePayrollRecords_Period" CHECK ("PeriodEnd" >= "PeriodStart"),
                  CONSTRAINT "CK_FinancePayrollRecords_Currency" CHECK (char_length("Currency") = 3)
                );
                ALTER TABLE "FinancePayrollRecords" ADD COLUMN IF NOT EXISTS "SalaryStructureId" uuid NULL;
                ALTER TABLE "FinancePayrollRecords" ADD COLUMN IF NOT EXISTS "DepartmentId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
                ALTER TABLE "FinancePayrollRecords" ADD COLUMN IF NOT EXISTS "EmployeeRole" varchar(30) NOT NULL DEFAULT 'Employee';
                ALTER TABLE "FinancePayrollRecords" ADD COLUMN IF NOT EXISTS "OvertimeHours" numeric(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE "FinancePayrollRecords" ADD COLUMN IF NOT EXISTS "OvertimePay" numeric(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE "FinancePayrollRecords" ADD COLUMN IF NOT EXISTS "GrossSalary" numeric(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE "FinancePayrollRecords" ADD COLUMN IF NOT EXISTS "CalculationSnapshotJson" text NOT NULL DEFAULT '{}';
                ALTER TABLE "FinancePayrollRecords" ADD COLUMN IF NOT EXISTS "AttendanceSnapshotJson" text NOT NULL DEFAULT '{}';
                CREATE TABLE IF NOT EXISTS "SalaryStructures" (
                  "Id" uuid PRIMARY KEY, "DepartmentId" uuid NOT NULL, "EmployeeRole" varchar(30) NOT NULL,
                  "MonthlyBasicSalary" numeric(18,2) NOT NULL, "OvertimeHourlyRate" numeric(18,2) NOT NULL,
                  "Currency" varchar(3) NOT NULL, "EffectiveFrom" date NOT NULL, "EffectiveTo" date NULL,
                  "Revision" integer NOT NULL, "IsApproved" boolean NOT NULL, "ApprovedByOwnerId" uuid NOT NULL,
                  "CreatedAtUtc" timestamptz NOT NULL, "UpdatedAtUtc" timestamptz NULL,
                  CONSTRAINT "CK_SalaryStructures_Amounts" CHECK ("MonthlyBasicSalary" > 0 AND "OvertimeHourlyRate" >= 0),
                  CONSTRAINT "CK_SalaryStructures_Role" CHECK ("EmployeeRole" IN ('Admin','Manager','Employee')),
                  CONSTRAINT "CK_SalaryStructures_Currency" CHECK (char_length("Currency") = 3),
                  CONSTRAINT "CK_SalaryStructures_Dates" CHECK ("EffectiveTo" IS NULL OR "EffectiveTo" >= "EffectiveFrom")
                );
                CREATE TABLE IF NOT EXISTS "EmployeeSalaryOverrides" (
                  "Id" uuid PRIMARY KEY, "EmployeeId" uuid NOT NULL, "MonthlyBasicSalary" numeric(18,2) NOT NULL,
                  "OvertimeHourlyRate" numeric(18,2) NULL, "Currency" varchar(3) NOT NULL,
                  "EffectiveFrom" date NOT NULL, "EffectiveTo" date NULL, "Reason" varchar(1000) NOT NULL,
                  "ApprovedByOwnerId" uuid NOT NULL, "CreatedAtUtc" timestamptz NOT NULL, "UpdatedAtUtc" timestamptz NULL,
                  CONSTRAINT "CK_EmployeeSalaryOverrides_Amounts" CHECK ("MonthlyBasicSalary" > 0 AND ("OvertimeHourlyRate" IS NULL OR "OvertimeHourlyRate" >= 0)),
                  CONSTRAINT "CK_EmployeeSalaryOverrides_Currency" CHECK (char_length("Currency") = 3),
                  CONSTRAINT "CK_EmployeeSalaryOverrides_Dates" CHECK ("EffectiveTo" IS NULL OR "EffectiveTo" >= "EffectiveFrom")
                );
                CREATE TABLE IF NOT EXISTS "SalaryAllowanceConfigurations" (
                  "Id" uuid PRIMARY KEY, "DepartmentId" uuid NULL, "EmployeeRole" varchar(30) NULL, "EmployeeId" uuid NULL,
                  "Kind" varchar(30) NOT NULL, "CalculationType" varchar(30) NOT NULL, "Value" numeric(18,2) NOT NULL,
                  "Currency" varchar(3) NOT NULL, "EffectiveFrom" date NOT NULL, "EffectiveTo" date NULL,
                  "ApprovedByOwnerId" uuid NOT NULL, "CreatedAtUtc" timestamptz NOT NULL, "UpdatedAtUtc" timestamptz NULL,
                  CONSTRAINT "CK_SalaryAllowances_Target" CHECK ("DepartmentId" IS NOT NULL OR "EmployeeId" IS NOT NULL),
                  CONSTRAINT "CK_SalaryAllowances_Value" CHECK ("Value" >= 0 AND ("CalculationType" <> 'PercentageOfBasic' OR "Value" <= 100)),
                  CONSTRAINT "CK_SalaryAllowances_Currency" CHECK (char_length("Currency") = 3)
                );
                CREATE TABLE IF NOT EXISTS "MockPayrollPayments" (
                  "Id" uuid PRIMARY KEY, "PayrollId" uuid NOT NULL REFERENCES "FinancePayrollRecords"("Id") ON DELETE RESTRICT,
                  "InstructionReference" varchar(80) NOT NULL, "IdempotencyKey" varchar(120) NOT NULL,
                  "Scenario" varchar(30) NOT NULL, "Status" varchar(30) NOT NULL, "Amount" numeric(18,2) NOT NULL,
                  "Currency" varchar(3) NOT NULL, "AttemptCount" integer NOT NULL, "MockProviderReference" varchar(120) NULL,
                  "FailureReason" varchar(500) NULL, "ConfirmedAtUtc" timestamptz NULL, "ProcessedByUserId" uuid NOT NULL,
                  "CreatedAtUtc" timestamptz NOT NULL, "UpdatedAtUtc" timestamptz NULL,
                  CONSTRAINT "CK_MockPayrollPayments_Amount" CHECK ("Amount" >= 0),
                  CONSTRAINT "CK_MockPayrollPayments_Currency" CHECK (char_length("Currency") = 3)
                );
                CREATE TABLE IF NOT EXISTS "SettlementReconciliations" (
                  "Id" uuid PRIMARY KEY, "SettlementId" uuid NOT NULL REFERENCES "SettlementRecords"("Id") ON DELETE RESTRICT,
                  "ProviderReportedAmount" numeric(18,2) NOT NULL, "ProviderFee" numeric(18,2) NOT NULL,
                  "BankReceivedAmount" numeric(18,2) NOT NULL, "DifferenceAmount" numeric(18,2) NOT NULL,
                  "Currency" varchar(3) NOT NULL, "Status" varchar(30) NOT NULL, "Notes" varchar(2000) NULL,
                  "PreparedByUserId" uuid NOT NULL, "ReviewedByUserId" uuid NULL, "CompletedAtUtc" timestamptz NULL,
                  "CreatedAtUtc" timestamptz NOT NULL, "UpdatedAtUtc" timestamptz NULL,
                  CONSTRAINT "CK_SettlementReconciliations_Amounts" CHECK ("ProviderReportedAmount" >= 0 AND "ProviderFee" >= 0 AND "BankReceivedAmount" >= 0),
                  CONSTRAINT "CK_SettlementReconciliations_Currency" CHECK (char_length("Currency") = 3)
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_FinanceInvoices_Number" ON "FinanceInvoices"("InvoiceNumber");
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_FinanceInvoices_Charge" ON "FinanceInvoices"("Type", "ChargeReference");
                CREATE INDEX IF NOT EXISTS "IX_FinanceInvoices_Outstanding" ON "FinanceInvoices"("DueDate") WHERE "Status" IN ('Issued', 'PartiallyPaid');
                CREATE INDEX IF NOT EXISTS "IX_FinanceInvoices_BookingId" ON "FinanceInvoices"("BookingId");
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_FinanceCreditNotes_Number" ON "FinanceCreditNotes"("CreditNoteNumber");
                CREATE INDEX IF NOT EXISTS "IX_FinanceCreditNotes_InvoiceId" ON "FinanceCreditNotes"("InvoiceId");
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_FinancePayrollRecords_Source" ON "FinancePayrollRecords"("SourcePayrollId");
                CREATE INDEX IF NOT EXISTS "IX_FinancePayrollRecords_EmployeePeriod" ON "FinancePayrollRecords"("EmployeeId", "PeriodEnd");
                CREATE INDEX IF NOT EXISTS "IX_FinancePayrollRecords_Pending" ON "FinancePayrollRecords"("PeriodEnd") WHERE "Status" IN ('PendingVerification', 'ManagerReview', 'OwnerApproval');
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_SalaryStructures_Revision" ON "SalaryStructures"("DepartmentId", "EmployeeRole", "EffectiveFrom");
                CREATE INDEX IF NOT EXISTS "IX_SalaryStructures_Active" ON "SalaryStructures"("DepartmentId", "EmployeeRole", "EffectiveTo");
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_EmployeeSalaryOverrides_Revision" ON "EmployeeSalaryOverrides"("EmployeeId", "EffectiveFrom");
                CREATE INDEX IF NOT EXISTS "IX_SalaryAllowances_Lookup" ON "SalaryAllowanceConfigurations"("DepartmentId", "EmployeeRole", "EmployeeId", "EffectiveTo");
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_MockPayrollPayments_Payroll" ON "MockPayrollPayments"("PayrollId");
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_MockPayrollPayments_Idempotency" ON "MockPayrollPayments"("IdempotencyKey");
                CREATE INDEX IF NOT EXISTS "IX_MockPayrollPayments_Status" ON "MockPayrollPayments"("Status", "CreatedAtUtc");
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_SettlementReconciliations_Settlement" ON "SettlementReconciliations"("SettlementId");
                CREATE INDEX IF NOT EXISTS "IX_SettlementReconciliations_Open" ON "SettlementReconciliations"("CreatedAtUtc") WHERE "Status" <> 'Completed';
                """);
        }
        await DataSeeder.SeedAsync(context);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not complete database initialization on startup. Ensure PostgreSQL is accessible if running full persistence.");
    }
}

app.Run();

public partial class Program { }

public interface IBookingJwksResolver
{
    Task<IEnumerable<SecurityKey>> GetSigningKeysAsync(string? kid = null);
}

public class BookingJwksResolver : IBookingJwksResolver
{
    private readonly HttpClient _httpClient;
    private readonly string _jwksUri;
    private readonly ILogger<BookingJwksResolver> _logger;
    private JsonWebKeySet? _cachedKeySet;
    private DateTime _lastFetch = DateTime.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public BookingJwksResolver(HttpClient httpClient, IConfiguration configuration, ILogger<BookingJwksResolver> logger)
    {
        _httpClient = httpClient;
        _jwksUri = configuration["Jwt:JwksUri"] ?? configuration["Authentication:JwksUri"] ?? "http://identity-service:5001/.well-known/jwks.json";
        _logger = logger;
    }

    public async Task<IEnumerable<SecurityKey>> GetSigningKeysAsync(string? kid = null)
    {
        if (_cachedKeySet != null && (DateTime.UtcNow - _lastFetch) < TimeSpan.FromMinutes(10))
        {
            var cachedKeys = _cachedKeySet.GetSigningKeys();
            if (string.IsNullOrEmpty(kid) || cachedKeys.Any(k => k.KeyId == kid))
            {
                return string.IsNullOrEmpty(kid) ? cachedKeys : cachedKeys.Where(k => k.KeyId == kid);
            }
        }

        await _lock.WaitAsync();
        try
        {
            if (_cachedKeySet != null && (DateTime.UtcNow - _lastFetch) < TimeSpan.FromSeconds(30))
            {
                var cachedKeys = _cachedKeySet.GetSigningKeys();
                if (string.IsNullOrEmpty(kid) || cachedKeys.Any(k => k.KeyId == kid))
                {
                    return string.IsNullOrEmpty(kid) ? cachedKeys : cachedKeys.Where(k => k.KeyId == kid);
                }
            }

            _logger.LogInformation("Fetching JWKS keys from {JwksUri}", _jwksUri);
            var response = await _httpClient.GetStringAsync(_jwksUri);
            _cachedKeySet = new JsonWebKeySet(response);
            _lastFetch = DateTime.UtcNow;

            var keys = _cachedKeySet.GetSigningKeys();
            return string.IsNullOrEmpty(kid) ? keys : keys.Where(k => k.KeyId == kid);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch JWKS from {JwksUri}. Using fallback/cached keys if available.", _jwksUri);
            var fallback = _cachedKeySet?.GetSigningKeys() ?? Enumerable.Empty<SecurityKey>();
            return string.IsNullOrEmpty(kid) ? fallback : fallback.Where(k => k.KeyId == kid);
        }
        finally
        {
            _lock.Release();
        }
    }
}
