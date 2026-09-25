CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

CREATE TABLE "BookingDrafts" (
    "Id" uuid NOT NULL,
    "CustomerId" uuid NOT NULL,
    "CustomerEmail" text NOT NULL,
    "RoomId" uuid NOT NULL,
    "RoomName" character varying(150) NOT NULL,
    "RoomTypeId" uuid NOT NULL,
    "RatePlanId" text NOT NULL,
    "RatePlanName" character varying(150) NOT NULL,
    "CheckInDate" date NOT NULL,
    "CheckOutDate" date NOT NULL,
    "GuestCount" integer NOT NULL,
    "RoomsCount" integer NOT NULL,
    "PricePerNight" numeric(18,2) NOT NULL,
    "Nights" integer NOT NULL,
    "TaxesAndFees" numeric(18,2) NOT NULL,
    "TotalAmount" numeric(18,2) NOT NULL,
    "IsUpgraded" boolean NOT NULL,
    "OriginalRoomId" uuid,
    "OriginalRoomName" text,
    "SessionSuppressedUpsell" boolean NOT NULL,
    "Status" character varying(50) NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_BookingDrafts" PRIMARY KEY ("Id")
);

CREATE TABLE "Bookings" (
    "Id" uuid NOT NULL,
    "BookingReference" character varying(50) NOT NULL,
    "CustomerId" uuid NOT NULL,
    "CustomerLastName" character varying(100) NOT NULL,
    "CustomerEmail" character varying(150) NOT NULL,
    "RoomId" uuid NOT NULL,
    "RoomNumber" text NOT NULL,
    "RoomTypeId" uuid NOT NULL,
    "CheckInDate" date NOT NULL,
    "CheckOutDate" date NOT NULL,
    "GuestCount" integer NOT NULL,
    "TotalAmount" numeric(18,2) NOT NULL,
    "Status" integer NOT NULL,
    "PaymentReference" character varying(100),
    "PayHereOrderId" character varying(100),
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_Bookings" PRIMARY KEY ("Id")
);

CREATE TABLE "Complaints" (
    "Id" uuid NOT NULL,
    "BookingId" uuid,
    "CustomerId" uuid NOT NULL,
    "Category" integer NOT NULL,
    "Severity" integer NOT NULL,
    "Status" integer NOT NULL,
    "Title" character varying(200) NOT NULL,
    "Description" character varying(4000) NOT NULL,
    "SlaDeadlineUtc" timestamp with time zone NOT NULL,
    "ResolutionNotes" character varying(2000),
    "ResolvedAtUtc" timestamp with time zone,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_Complaints" PRIMARY KEY ("Id")
);

CREATE TABLE "EmployeeSalaryOverrides" (
    "Id" uuid NOT NULL,
    "EmployeeId" uuid NOT NULL,
    "MonthlyBasicSalary" numeric(18,2) NOT NULL,
    "OvertimeHourlyRate" numeric(18,2),
    "Currency" character varying(3) NOT NULL,
    "EffectiveFrom" date NOT NULL,
    "EffectiveTo" date,
    "Reason" character varying(1000) NOT NULL,
    "ApprovedByOwnerId" uuid NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_EmployeeSalaryOverrides" PRIMARY KEY ("Id")
);

CREATE TABLE "ExpenseRequests" (
    "Id" uuid NOT NULL,
    "Amount" numeric(18,2) NOT NULL,
    "Currency" character varying(3) NOT NULL,
    "Description" character varying(1000) NOT NULL,
    "Status" character varying(20) NOT NULL,
    "ApprovalStage" character varying(20) NOT NULL,
    "ExecutionStatus" character varying(20) NOT NULL,
    "SubmittedByUserId" uuid NOT NULL,
    "SubmittedByDepartmentId" uuid NOT NULL,
    "IdempotencyKey" character varying(100) NOT NULL,
    "DecidedByUserId" uuid,
    "DecidedAtUtc" timestamp with time zone,
    "DecisionReason" character varying(1000),
    "Category" character varying(100) NOT NULL,
    "VendorReference" character varying(200),
    "ReceiptUrl" character varying(1000),
    "VerificationStatus" character varying(20) NOT NULL,
    "VerifiedByUserId" uuid,
    "VerifiedAtUtc" timestamp with time zone,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_ExpenseRequests" PRIMARY KEY ("Id")
);

CREATE TABLE "FinanceAccessGrants" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Permission" character varying(100) NOT NULL,
    "GrantedByUserId" uuid NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_FinanceAccessGrants" PRIMARY KEY ("Id")
);

CREATE TABLE "FinanceApprovalSettings" (
    "Id" uuid NOT NULL,
    "ManagerExpenseApprovalLimit" numeric(18,2) NOT NULL,
    "ManagerRefundApprovalLimit" numeric(18,2) NOT NULL,
    "ManagerReleaseApprovalLimit" numeric(18,2) NOT NULL,
    "Currency" character varying(3) NOT NULL,
    "UpdatedByUserId" uuid NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_FinanceApprovalSettings" PRIMARY KEY ("Id")
);

CREATE TABLE "FinanceAuditLogs" (
    "Id" uuid NOT NULL,
    "ActorUserId" uuid NOT NULL,
    "Action" character varying(100) NOT NULL,
    "EntityType" character varying(100) NOT NULL,
    "EntityId" uuid NOT NULL,
    "Details" character varying(2000) NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_FinanceAuditLogs" PRIMARY KEY ("Id")
);

CREATE TABLE "FinanceInvoices" (
    "Id" uuid NOT NULL,
    "InvoiceNumber" character varying(40) NOT NULL,
    "Type" character varying(20) NOT NULL,
    "Status" character varying(20) NOT NULL,
    "BookingId" uuid,
    "PaymentId" uuid,
    "ChargeReference" character varying(150) NOT NULL,
    "PartyName" character varying(200) NOT NULL,
    "PartyEmail" character varying(254) NOT NULL,
    "Subtotal" numeric(18,2) NOT NULL,
    "TaxAmount" numeric(18,2) NOT NULL,
    "TotalAmount" numeric(18,2) NOT NULL,
    "PaidAmount" numeric(18,2) NOT NULL,
    "Currency" character varying(3) NOT NULL,
    "IssueDate" date NOT NULL,
    "DueDate" date NOT NULL,
    "CreatedByUserId" uuid NOT NULL,
    "ValidatedByUserId" uuid,
    "IssuedAtUtc" timestamp with time zone,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_FinanceInvoices" PRIMARY KEY ("Id")
);

CREATE TABLE "FinancePayrollRecords" (
    "Id" uuid NOT NULL,
    "SourcePayrollId" uuid NOT NULL,
    "EmployeeId" uuid NOT NULL,
    "PeriodStart" date NOT NULL,
    "PeriodEnd" date NOT NULL,
    "BaseSalary" numeric(18,2) NOT NULL,
    "Allowances" numeric(18,2) NOT NULL,
    "Deductions" numeric(18,2) NOT NULL,
    "NetSalary" numeric(18,2) NOT NULL,
    "Currency" character varying(3) NOT NULL,
    "Status" character varying(30) NOT NULL,
    "SubmittedByUserId" uuid NOT NULL,
    "VerifiedByUserId" uuid,
    "ManagerApprovedByUserId" uuid,
    "OwnerApprovedByUserId" uuid,
    "DisbursedAtUtc" timestamp with time zone,
    "DisbursementReference" character varying(200),
    "SalaryStructureId" uuid,
    "DepartmentId" uuid NOT NULL,
    "EmployeeRole" character varying(50) NOT NULL,
    "OvertimeHours" numeric(10,2) NOT NULL,
    "OvertimePay" numeric(18,2) NOT NULL,
    "GrossSalary" numeric(18,2) NOT NULL,
    "CalculationSnapshotJson" text NOT NULL,
    "AttendanceSnapshotJson" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_FinancePayrollRecords" PRIMARY KEY ("Id")
);

CREATE TABLE "FrontOfficeAuditLogs" (
    "Id" uuid NOT NULL,
    "BookingId" uuid NOT NULL,
    "BookingReference" character varying(50) NOT NULL,
    "Action" character varying(50) NOT NULL,
    "ActorUserId" uuid,
    "ActorRole" character varying(50) NOT NULL,
    "Source" character varying(50) NOT NULL,
    "Reason" character varying(1000),
    "PreviousState" character varying(100) NOT NULL,
    "NewState" character varying(100) NOT NULL,
    "Details" character varying(2000),
    "TimestampUtc" timestamp with time zone NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_FrontOfficeAuditLogs" PRIMARY KEY ("Id")
);

CREATE TABLE "OutboxMessages" (
    "Id" uuid NOT NULL,
    "Type" character varying(100) NOT NULL,
    "Content" text NOT NULL,
    "OccurredOnUtc" timestamp with time zone NOT NULL,
    "ProcessedOnUtc" timestamp with time zone,
    "Error" text,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_OutboxMessages" PRIMARY KEY ("Id")
);

CREATE TABLE "PaymentApprovalHistory" (
    "Id" uuid NOT NULL,
    "RequestId" uuid NOT NULL,
    "RequestType" character varying(30) NOT NULL,
    "ActorUserId" uuid NOT NULL,
    "Stage" character varying(20) NOT NULL,
    "Action" character varying(20) NOT NULL,
    "Reason" character varying(1000),
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_PaymentApprovalHistory" PRIMARY KEY ("Id")
);

CREATE TABLE "PaymentWebhookReceipts" (
    "Id" uuid NOT NULL,
    "Provider" integer NOT NULL,
    "ProviderEventId" character varying(200) NOT NULL,
    "EventType" character varying(50) NOT NULL,
    "PayloadHash" character varying(64) NOT NULL,
    "ProcessedAtUtc" timestamp with time zone NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_PaymentWebhookReceipts" PRIMARY KEY ("Id")
);

CREATE TABLE "SalaryAllowanceConfigurations" (
    "Id" uuid NOT NULL,
    "DepartmentId" uuid,
    "EmployeeRole" character varying(50),
    "EmployeeId" uuid,
    "Kind" character varying(30) NOT NULL,
    "CalculationType" character varying(30) NOT NULL,
    "Value" numeric(18,4) NOT NULL,
    "Currency" character varying(3) NOT NULL,
    "EffectiveFrom" date NOT NULL,
    "EffectiveTo" date,
    "ApprovedByOwnerId" uuid NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_SalaryAllowanceConfigurations" PRIMARY KEY ("Id")
);

CREATE TABLE "SalaryStructures" (
    "Id" uuid NOT NULL,
    "DepartmentId" uuid NOT NULL,
    "EmployeeRole" character varying(50) NOT NULL,
    "MonthlyBasicSalary" numeric(18,2) NOT NULL,
    "OvertimeHourlyRate" numeric(18,2) NOT NULL,
    "Currency" character varying(3) NOT NULL,
    "EffectiveFrom" date NOT NULL,
    "EffectiveTo" date,
    "Revision" integer NOT NULL,
    "IsApproved" boolean NOT NULL,
    "ApprovedByOwnerId" uuid NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_SalaryStructures" PRIMARY KEY ("Id")
);

CREATE TABLE "Payments" (
    "Id" uuid NOT NULL,
    "BookingId" uuid NOT NULL,
    "Amount" numeric(18,2) NOT NULL,
    "Currency" character varying(10) NOT NULL,
    "Provider" integer NOT NULL,
    "PayHereOrderId" character varying(100) NOT NULL,
    "PayHerePaymentId" character varying(100),
    "ProviderPaymentReference" character varying(200),
    "ProviderCheckoutReference" character varying(200),
    "RefundedAmount" numeric(18,2) NOT NULL,
    "Status" integer NOT NULL,
    "CapturedAt" timestamp with time zone,
    "RefundedAt" timestamp with time zone,
    "RefundAmount" numeric(18,2),
    "RefundReason" character varying(500),
    "FundsHeldAtUtc" timestamp with time zone,
    "ReleasedAtUtc" timestamp with time zone,
    "SettledAtUtc" timestamp with time zone,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_Payments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Payments_Bookings_BookingId" FOREIGN KEY ("BookingId") REFERENCES "Bookings" ("Id") ON DELETE CASCADE
);

CREATE TABLE "Reviews" (
    "Id" uuid NOT NULL,
    "BookingId" uuid NOT NULL,
    "CustomerId" uuid NOT NULL,
    "RoomTypeId" uuid NOT NULL,
    "Rating" integer NOT NULL,
    "Comment" character varying(2000) NOT NULL,
    "IsPublished" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_Reviews" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Reviews_Bookings_BookingId" FOREIGN KEY ("BookingId") REFERENCES "Bookings" ("Id") ON DELETE CASCADE
);

CREATE TABLE "ComplaintTimelineEntries" (
    "Id" uuid NOT NULL,
    "ComplaintId" uuid NOT NULL,
    "FromStatus" integer NOT NULL,
    "ToStatus" integer NOT NULL,
    "Note" character varying(2000) NOT NULL,
    "ChangedBy" character varying(100) NOT NULL,
    "TimestampUtc" timestamp with time zone NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_ComplaintTimelineEntries" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ComplaintTimelineEntries_Complaints_ComplaintId" FOREIGN KEY ("ComplaintId") REFERENCES "Complaints" ("Id") ON DELETE CASCADE
);

CREATE TABLE "FinanceCreditNotes" (
    "Id" uuid NOT NULL,
    "InvoiceId" uuid NOT NULL,
    "CreditNoteNumber" character varying(40) NOT NULL,
    "Amount" numeric(18,2) NOT NULL,
    "Currency" character varying(3) NOT NULL,
    "Reason" character varying(1000) NOT NULL,
    "Status" character varying(20) NOT NULL,
    "CreatedByUserId" uuid NOT NULL,
    "AuthorizedByUserId" uuid,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_FinanceCreditNotes" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_FinanceCreditNotes_FinanceInvoices_InvoiceId" FOREIGN KEY ("InvoiceId") REFERENCES "FinanceInvoices" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "MockPayrollPayments" (
    "Id" uuid NOT NULL,
    "PayrollId" uuid NOT NULL,
    "InstructionReference" character varying(80) NOT NULL,
    "IdempotencyKey" character varying(100) NOT NULL,
    "Scenario" character varying(30) NOT NULL,
    "Status" character varying(30) NOT NULL,
    "Amount" numeric(18,2) NOT NULL,
    "Currency" character varying(3) NOT NULL,
    "AttemptCount" integer NOT NULL,
    "MockProviderReference" character varying(100),
    "FailureReason" character varying(500),
    "ConfirmedAtUtc" timestamp with time zone,
    "ProcessedByUserId" uuid NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_MockPayrollPayments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_MockPayrollPayments_FinancePayrollRecords_PayrollId" FOREIGN KEY ("PayrollId") REFERENCES "FinancePayrollRecords" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "EscrowLifecycleRecords" (
    "Id" uuid NOT NULL,
    "PaymentId" uuid NOT NULL,
    "EventType" character varying(30) NOT NULL,
    "ProviderReference" character varying(200) NOT NULL,
    "IdempotencyKey" character varying(150) NOT NULL,
    "Details" character varying(2000) NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_EscrowLifecycleRecords" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_EscrowLifecycleRecords_Payments_PaymentId" FOREIGN KEY ("PaymentId") REFERENCES "Payments" ("Id") ON DELETE CASCADE
);

CREATE TABLE "EscrowReleaseRequests" (
    "Id" uuid NOT NULL,
    "Amount" numeric(18,2) NOT NULL,
    "Currency" character varying(3) NOT NULL,
    "Description" character varying(1000) NOT NULL,
    "Status" character varying(20) NOT NULL,
    "ApprovalStage" character varying(20) NOT NULL,
    "ExecutionStatus" character varying(20) NOT NULL,
    "SubmittedByUserId" uuid NOT NULL,
    "SubmittedByDepartmentId" uuid NOT NULL,
    "IdempotencyKey" character varying(100) NOT NULL,
    "DecidedByUserId" uuid,
    "DecidedAtUtc" timestamp with time zone,
    "DecisionReason" character varying(1000),
    "PaymentId" uuid NOT NULL,
    "ProviderReleaseReference" character varying(200),
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_EscrowReleaseRequests" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_EscrowReleaseRequests_Payments_PaymentId" FOREIGN KEY ("PaymentId") REFERENCES "Payments" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "RefundRequests" (
    "Id" uuid NOT NULL,
    "Amount" numeric(18,2) NOT NULL,
    "Currency" character varying(3) NOT NULL,
    "Description" character varying(1000) NOT NULL,
    "Status" character varying(20) NOT NULL,
    "ApprovalStage" character varying(20) NOT NULL,
    "ExecutionStatus" character varying(20) NOT NULL,
    "SubmittedByUserId" uuid NOT NULL,
    "SubmittedByDepartmentId" uuid NOT NULL,
    "IdempotencyKey" character varying(100) NOT NULL,
    "DecidedByUserId" uuid,
    "DecidedAtUtc" timestamp with time zone,
    "DecisionReason" character varying(1000),
    "PaymentId" uuid NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_RefundRequests" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_RefundRequests_Payments_PaymentId" FOREIGN KEY ("PaymentId") REFERENCES "Payments" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "SettlementRecords" (
    "Id" uuid NOT NULL,
    "PaymentId" uuid NOT NULL,
    "Amount" numeric(18,2) NOT NULL,
    "Currency" character varying(3) NOT NULL,
    "Status" character varying(20) NOT NULL,
    "ProviderSettlementReference" character varying(200) NOT NULL,
    "ConfirmedAtUtc" timestamp with time zone,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_SettlementRecords" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_SettlementRecords_Payments_PaymentId" FOREIGN KEY ("PaymentId") REFERENCES "Payments" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "SettlementReconciliations" (
    "Id" uuid NOT NULL,
    "SettlementId" uuid NOT NULL,
    "ProviderReportedAmount" numeric(18,2) NOT NULL,
    "ProviderFee" numeric(18,2) NOT NULL,
    "BankReceivedAmount" numeric(18,2) NOT NULL,
    "DifferenceAmount" numeric(18,2) NOT NULL,
    "Currency" character varying(3) NOT NULL,
    "Status" character varying(30) NOT NULL,
    "Notes" character varying(2000),
    "PreparedByUserId" uuid NOT NULL,
    "ReviewedByUserId" uuid,
    "CompletedAtUtc" timestamp with time zone,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_SettlementReconciliations" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_SettlementReconciliations_SettlementRecords_SettlementId" FOREIGN KEY ("SettlementId") REFERENCES "SettlementRecords" ("Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_Bookings_BookingReference" ON "Bookings" ("BookingReference");

CREATE INDEX "IX_Bookings_CustomerId" ON "Bookings" ("CustomerId");

CREATE INDEX "IX_Bookings_RoomId_CheckInDate_CheckOutDate" ON "Bookings" ("RoomId", "CheckInDate", "CheckOutDate");

CREATE INDEX "IX_Bookings_Status" ON "Bookings" ("Status");

CREATE INDEX "IX_Complaints_BookingId" ON "Complaints" ("BookingId");

CREATE INDEX "IX_Complaints_CustomerId" ON "Complaints" ("CustomerId");

CREATE INDEX "IX_Complaints_SlaDeadlineUtc" ON "Complaints" ("SlaDeadlineUtc");

CREATE INDEX "IX_Complaints_Status" ON "Complaints" ("Status");

CREATE INDEX "IX_ComplaintTimelineEntries_ComplaintId" ON "ComplaintTimelineEntries" ("ComplaintId");

CREATE UNIQUE INDEX "IX_EmployeeSalaryOverrides_EmployeeId_EffectiveFrom" ON "EmployeeSalaryOverrides" ("EmployeeId", "EffectiveFrom");

CREATE INDEX "IX_EmployeeSalaryOverrides_EmployeeId_EffectiveTo" ON "EmployeeSalaryOverrides" ("EmployeeId", "EffectiveTo");

CREATE UNIQUE INDEX "IX_EscrowLifecycleRecords_IdempotencyKey" ON "EscrowLifecycleRecords" ("IdempotencyKey");

CREATE INDEX "IX_EscrowLifecycleRecords_PaymentId_CreatedAtUtc" ON "EscrowLifecycleRecords" ("PaymentId", "CreatedAtUtc");

CREATE UNIQUE INDEX "IX_EscrowReleaseRequests_IdempotencyKey" ON "EscrowReleaseRequests" ("IdempotencyKey");

CREATE UNIQUE INDEX "IX_EscrowReleaseRequests_PaymentId" ON "EscrowReleaseRequests" ("PaymentId");

CREATE INDEX "IX_EscrowReleaseRequests_Status_ApprovalStage_CreatedAtUtc" ON "EscrowReleaseRequests" ("Status", "ApprovalStage", "CreatedAtUtc");

CREATE UNIQUE INDEX "IX_ExpenseRequests_IdempotencyKey" ON "ExpenseRequests" ("IdempotencyKey");

CREATE INDEX "IX_ExpenseRequests_Status_ApprovalStage_CreatedAtUtc" ON "ExpenseRequests" ("Status", "ApprovalStage", "CreatedAtUtc");

CREATE INDEX "IX_ExpenseRequests_SubmittedByDepartmentId" ON "ExpenseRequests" ("SubmittedByDepartmentId");

CREATE INDEX "IX_FinanceAccessGrants_UserId_IsActive" ON "FinanceAccessGrants" ("UserId", "IsActive");

CREATE UNIQUE INDEX "IX_FinanceAccessGrants_UserId_Permission" ON "FinanceAccessGrants" ("UserId", "Permission");

CREATE INDEX "IX_FinanceAuditLogs_ActorUserId_CreatedAtUtc" ON "FinanceAuditLogs" ("ActorUserId", "CreatedAtUtc");

CREATE INDEX "IX_FinanceAuditLogs_EntityType_EntityId_CreatedAtUtc" ON "FinanceAuditLogs" ("EntityType", "EntityId", "CreatedAtUtc");

CREATE UNIQUE INDEX "IX_FinanceCreditNotes_CreditNoteNumber" ON "FinanceCreditNotes" ("CreditNoteNumber");

CREATE INDEX "IX_FinanceCreditNotes_InvoiceId" ON "FinanceCreditNotes" ("InvoiceId");

CREATE INDEX "IX_FinanceInvoices_BookingId" ON "FinanceInvoices" ("BookingId");

CREATE UNIQUE INDEX "IX_FinanceInvoices_InvoiceNumber" ON "FinanceInvoices" ("InvoiceNumber");

CREATE INDEX "IX_FinanceInvoices_Status_DueDate" ON "FinanceInvoices" ("Status", "DueDate");

CREATE UNIQUE INDEX "IX_FinanceInvoices_Type_ChargeReference" ON "FinanceInvoices" ("Type", "ChargeReference");

CREATE INDEX "IX_FinancePayrollRecords_EmployeeId_PeriodEnd" ON "FinancePayrollRecords" ("EmployeeId", "PeriodEnd");

CREATE UNIQUE INDEX "IX_FinancePayrollRecords_SourcePayrollId" ON "FinancePayrollRecords" ("SourcePayrollId");

CREATE INDEX "IX_FinancePayrollRecords_Status_PeriodEnd" ON "FinancePayrollRecords" ("Status", "PeriodEnd");

CREATE INDEX "IX_FrontOfficeAuditLogs_Action" ON "FrontOfficeAuditLogs" ("Action");

CREATE INDEX "IX_FrontOfficeAuditLogs_BookingId" ON "FrontOfficeAuditLogs" ("BookingId");

CREATE INDEX "IX_FrontOfficeAuditLogs_TimestampUtc" ON "FrontOfficeAuditLogs" ("TimestampUtc");

CREATE UNIQUE INDEX "IX_MockPayrollPayments_IdempotencyKey" ON "MockPayrollPayments" ("IdempotencyKey");

CREATE UNIQUE INDEX "IX_MockPayrollPayments_PayrollId" ON "MockPayrollPayments" ("PayrollId");

CREATE INDEX "IX_MockPayrollPayments_Status_CreatedAtUtc" ON "MockPayrollPayments" ("Status", "CreatedAtUtc");

CREATE INDEX "IX_OutboxMessages_ProcessedOnUtc" ON "OutboxMessages" ("ProcessedOnUtc");

CREATE INDEX "IX_PaymentApprovalHistory_RequestId_CreatedAtUtc" ON "PaymentApprovalHistory" ("RequestId", "CreatedAtUtc");

CREATE INDEX "IX_Payments_BookingId" ON "Payments" ("BookingId");

CREATE INDEX "IX_Payments_PayHereOrderId" ON "Payments" ("PayHereOrderId");

CREATE UNIQUE INDEX "IX_Payments_PayHerePaymentId" ON "Payments" ("PayHerePaymentId") WHERE "PayHerePaymentId" IS NOT NULL;

CREATE UNIQUE INDEX "IX_Payments_Provider_PayHereOrderId" ON "Payments" ("Provider", "PayHereOrderId");

CREATE UNIQUE INDEX "IX_Payments_Provider_ProviderPaymentReference" ON "Payments" ("Provider", "ProviderPaymentReference") WHERE "ProviderPaymentReference" IS NOT NULL;

CREATE INDEX "IX_Payments_Status" ON "Payments" ("Status");

CREATE UNIQUE INDEX "IX_PaymentWebhookReceipts_Provider_ProviderEventId" ON "PaymentWebhookReceipts" ("Provider", "ProviderEventId");

CREATE UNIQUE INDEX "IX_RefundRequests_IdempotencyKey" ON "RefundRequests" ("IdempotencyKey");

CREATE INDEX "IX_RefundRequests_PaymentId" ON "RefundRequests" ("PaymentId");

CREATE INDEX "IX_RefundRequests_Status_ApprovalStage_CreatedAtUtc" ON "RefundRequests" ("Status", "ApprovalStage", "CreatedAtUtc");

CREATE UNIQUE INDEX "IX_Reviews_BookingId" ON "Reviews" ("BookingId");

CREATE INDEX "IX_Reviews_CustomerId" ON "Reviews" ("CustomerId");

CREATE INDEX "IX_Reviews_IsPublished" ON "Reviews" ("IsPublished");

CREATE INDEX "IX_Reviews_RoomTypeId" ON "Reviews" ("RoomTypeId");

CREATE INDEX "IX_SalaryAllowanceConfigurations_DepartmentId_EmployeeRole_Eff~" ON "SalaryAllowanceConfigurations" ("DepartmentId", "EmployeeRole", "EffectiveTo");

CREATE UNIQUE INDEX "IX_SalaryAllowanceConfigurations_DepartmentId_EmployeeRole_Emp~" ON "SalaryAllowanceConfigurations" ("DepartmentId", "EmployeeRole", "EmployeeId", "Kind", "EffectiveFrom");

CREATE INDEX "IX_SalaryAllowanceConfigurations_EmployeeId_EffectiveTo" ON "SalaryAllowanceConfigurations" ("EmployeeId", "EffectiveTo");

CREATE UNIQUE INDEX "IX_SalaryStructures_DepartmentId_EmployeeRole_EffectiveFrom" ON "SalaryStructures" ("DepartmentId", "EmployeeRole", "EffectiveFrom");

CREATE INDEX "IX_SalaryStructures_DepartmentId_EmployeeRole_EffectiveTo" ON "SalaryStructures" ("DepartmentId", "EmployeeRole", "EffectiveTo");

CREATE UNIQUE INDEX "IX_SettlementReconciliations_SettlementId" ON "SettlementReconciliations" ("SettlementId");

CREATE INDEX "IX_SettlementReconciliations_Status_CreatedAtUtc" ON "SettlementReconciliations" ("Status", "CreatedAtUtc");

CREATE INDEX "IX_SettlementRecords_PaymentId" ON "SettlementRecords" ("PaymentId");

CREATE UNIQUE INDEX "IX_SettlementRecords_ProviderSettlementReference" ON "SettlementRecords" ("ProviderSettlementReference");

CREATE INDEX "IX_SettlementRecords_Status_CreatedAtUtc" ON "SettlementRecords" ("Status", "CreatedAtUtc");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260918172955_InitialVersionedSchema', '8.0.8');

COMMIT;

START TRANSACTION;

ALTER TABLE "Bookings" ADD "CheckoutInitiatedAtUtc" timestamp with time zone;

UPDATE "Bookings" SET "CheckoutInitiatedAtUtc" = "CreatedAtUtc" WHERE "Status" = 0 AND "CheckoutInitiatedAtUtc" IS NULL;

CREATE INDEX "IX_Bookings_PendingPayment_CheckoutInitiated" ON "Bookings" ("CheckoutInitiatedAtUtc") WHERE "Status" = 0;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260922180000_AddCheckoutInitiatedAtUtcAndBackfill', '8.0.8');

COMMIT;

