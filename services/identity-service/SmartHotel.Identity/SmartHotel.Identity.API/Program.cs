using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SmartHotel.Identity.API.Middleware;
using SmartHotel.Identity.Application;
using SmartHotel.Identity.Application.Interfaces;
using SmartHotel.Identity.Infrastructure;
using SmartHotel.Identity.Infrastructure.Persistence;
using SmartHotel.Identity.Infrastructure.Services;
using SmartHotel.Authorization;

var builder = WebApplication.CreateBuilder(args);

// 1. Add services to container
builder.Services.AddControllers();
builder.Services.AddHotelDepartmentAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();

// 2. Swagger with Bearer token authentication
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SmartHotel Identity Service API",
        Version = "v1",
        Description = "Authentication and Identity Service for SmartHotel Backend (RS256 JWT, TPT Persistence)"
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
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

// 4. RS256 JWT Authentication configuration
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<RsaKeyManager, IConfiguration>((options, keyManager, configuration) =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = configuration["Jwt:Issuer"] ?? "SmartHotel.Identity",
            ValidateAudience = true,
            ValidAudience = configuration["Jwt:Audience"] ?? "SmartHotel.Clients",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = keyManager.GetSecurityKey(),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

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
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SmartHotel Identity API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors();

app.UseAuthentication();
app.UseMiddleware<ActiveEmployeeMiddleware>();
app.UseMiddleware<MustChangePasswordMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// Database initialization & Startup seeding
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        if (context.Database.IsRelational())
        {
            await context.Database.ExecuteSqlRawAsync("CREATE SCHEMA IF NOT EXISTS identity;");
            var creator = Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions
                .GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>(context.Database);
            try
            {
                await creator.CreateTablesAsync();
                logger.LogInformation("Database tables created successfully in 'identity' schema.");
            }
            catch (Exception ex)
            {
                logger.LogInformation("CreateTablesAsync info (tables may already exist): {Message}", ex.Message);
            }

            // Idempotent column additions — safe to run on every startup
            try
            {
                await context.Database.ExecuteSqlRawAsync(@"
                    ALTER TABLE identity.""Customers""
                    ADD COLUMN IF NOT EXISTS ""Role"" varchar(50) NOT NULL DEFAULT 'Guest';
                ");
                logger.LogInformation("Ensured 'Role' column exists on identity.Customers.");
            }
            catch (Exception ex)
            {
                logger.LogWarning("Could not apply column migration for Customers.Role: {Message}", ex.Message);
            }

            // ── Employee approval workflow columns ──────────────────────────
            try
            {
                await context.Database.ExecuteSqlRawAsync(@"
                    ALTER TABLE identity.""Employees""
                    ADD COLUMN IF NOT EXISTS ""Status"" varchar(30) NOT NULL DEFAULT 'Active';

                    ALTER TABLE identity.""Employees""
                    ADD COLUMN IF NOT EXISTS ""CreatedByEmployeeId"" uuid NULL;

                    ALTER TABLE identity.""Employees""
                    ADD COLUMN IF NOT EXISTS ""ReviewedByEmployeeId"" uuid NULL;

                    ALTER TABLE identity.""Employees""
                    ADD COLUMN IF NOT EXISTS ""ReviewedAtUtc"" timestamp with time zone NULL;

                    ALTER TABLE identity.""Employees""
                    ADD COLUMN IF NOT EXISTS ""RejectionReason"" varchar(500) NULL;

                    ALTER TABLE identity.""Employees""
                    ADD COLUMN IF NOT EXISTS ""MustChangePassword"" boolean NOT NULL DEFAULT false;

                    ALTER TABLE identity.""Employees"" ADD COLUMN IF NOT EXISTS ""NationalId"" varchar(50) NULL;
                    ALTER TABLE identity.""Employees"" ADD COLUMN IF NOT EXISTS ""NicPhotoUrl"" varchar(1000) NULL;
                    ALTER TABLE identity.""Employees"" ADD COLUMN IF NOT EXISTS ""ProfilePhotoUrl"" varchar(1000) NULL;
                    ALTER TABLE identity.""Employees"" ADD COLUMN IF NOT EXISTS ""BankName"" varchar(150) NULL;
                    ALTER TABLE identity.""Employees"" ADD COLUMN IF NOT EXISTS ""BankAccountName"" varchar(200) NULL;
                    ALTER TABLE identity.""Employees"" ADD COLUMN IF NOT EXISTS ""BankAccountNumber"" varchar(100) NULL;
                    ALTER TABLE identity.""Employees"" ADD COLUMN IF NOT EXISTS ""BankBranch"" varchar(150) NULL;
                    ALTER TABLE identity.""Employees"" ADD COLUMN IF NOT EXISTS ""SuspensionReason"" varchar(500) NULL;
                    ALTER TABLE identity.""Employees"" ADD COLUMN IF NOT EXISTS ""SuspendedAtUtc"" timestamp with time zone NULL;
                    ALTER TABLE identity.""Employees"" ADD COLUMN IF NOT EXISTS ""RequiresProfileCompletion"" boolean NOT NULL DEFAULT false;
                    ALTER TABLE identity.""Employees"" ADD COLUMN IF NOT EXISTS ""ProfileCompletionDeadlineUtc"" timestamp with time zone NULL;
                    ALTER TABLE identity.""Employees"" ADD COLUMN IF NOT EXISTS ""ProfileCompletedAtUtc"" timestamp with time zone NULL;
                    ALTER TABLE identity.""Employees"" ADD COLUMN IF NOT EXISTS ""DepartmentRoleSlot"" varchar(100) NULL;
                    ALTER TABLE identity.""Employees"" ADD COLUMN IF NOT EXISTS ""Designation"" varchar(100) NULL;
                ");
                logger.LogInformation("Ensured approval workflow and MustChangePassword columns exist on identity.Employees.");
            }
            catch (Exception ex)
            {
                logger.LogWarning("Could not apply column migration for Employees approval workflow: {Message}", ex.Message);
            }

            try
            {
                await context.Database.ExecuteSqlRawAsync(@"
                    ALTER TABLE identity.""Departments""
                    ADD COLUMN IF NOT EXISTS ""Description"" varchar(500) NOT NULL DEFAULT '';

                    INSERT INTO identity.""Departments"" (""Id"", ""Name"", ""Description"", ""CreatedAtUtc"") VALUES
                      ('22222222-2222-2222-2222-222222222220', 'Unassigned — migration review required', 'Employees backfilled during the one-department migration. Reassign each employee to an operational department.', now()),
                      ('22222222-2222-2222-2222-222222222221', 'Front Desk', 'Guest check-in, concierge, and reservations reception', now()),
                      ('22222222-2222-2222-2222-222222222222', 'Housekeeping', 'Room cleaning, linen inspection, and sanitization', now()),
                      ('22222222-2222-2222-2222-222222222223', 'Maintenance', 'Facility repairs, HVAC, electrical, and plumbing', now())
                      ,('22222222-2222-2222-2222-222222222224', 'Food & Beverage', 'Kitchen, restaurant, room service, and beverage operations', now())
                      ,('22222222-2222-2222-2222-222222222225', 'Finance', 'Revenue, expenses, refunds, payroll review, reporting, and financial controls', now())
                    ON CONFLICT (""Id"") DO NOTHING;

                    DO $migration$
                    DECLARE unassigned_count integer;
                    BEGIN
                      SELECT count(*) INTO unassigned_count FROM identity.""Employees"" WHERE ""DepartmentId"" IS NULL;
                      IF unassigned_count > 0 THEN
                        RAISE WARNING 'Backfilling % employees into Unassigned — migration review required.', unassigned_count;
                      END IF;
                    END $migration$;

                    UPDATE identity.""Employees""
                    SET ""DepartmentId"" = '22222222-2222-2222-2222-222222222220'
                    WHERE ""DepartmentId"" IS NULL;

                    ALTER TABLE identity.""Employees"" ALTER COLUMN ""DepartmentId"" SET NOT NULL;

                    DO $migration$
                    DECLARE duplicate_count integer;
                    BEGIN
                      SELECT count(*) INTO duplicate_count
                      FROM (SELECT ""ManagerId"" FROM identity.""Departments"" WHERE ""ManagerId"" IS NOT NULL GROUP BY ""ManagerId"" HAVING count(*) > 1) duplicates;
                      IF duplicate_count > 0 THEN
                        RAISE WARNING 'Found % managers assigned to multiple departments. Keeping each manager on their oldest department and clearing duplicate assignments.', duplicate_count;
                      END IF;
                    END $migration$;

                    WITH ranked AS (
                      SELECT ""Id"", row_number() OVER (PARTITION BY ""ManagerId"" ORDER BY ""CreatedAtUtc"", ""Id"") AS assignment_rank
                      FROM identity.""Departments""
                      WHERE ""ManagerId"" IS NOT NULL
                    )
                    UPDATE identity.""Departments"" d SET ""ManagerId"" = NULL
                    FROM ranked r WHERE d.""Id"" = r.""Id"" AND r.assignment_rank > 1;

                    CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Departments_ManagerId""
                      ON identity.""Departments"" (""ManagerId"") WHERE ""ManagerId"" IS NOT NULL;

                    ALTER TABLE identity.""Employees"" DROP CONSTRAINT IF EXISTS ""FK_Employees_Departments_DepartmentId"";
                    ALTER TABLE identity.""Employees"" ADD CONSTRAINT ""FK_Employees_Departments_DepartmentId""
                      FOREIGN KEY (""DepartmentId"") REFERENCES identity.""Departments"" (""Id"") ON DELETE RESTRICT;
                ");
                logger.LogWarning("Existing employees without a department were backfilled to 'Unassigned — migration review required'; review that department in the dashboard.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not apply the department ownership migration.");
            }

            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS identity.""ApprovalRequests"" (
                    ""Id"" uuid PRIMARY KEY,
                    ""Type"" varchar(100) NOT NULL,
                    ""Description"" varchar(1000) NOT NULL,
                    ""RequestedBy"" varchar(200) NOT NULL,
                    ""Status"" varchar(30) NOT NULL DEFAULT 'PendingApproval',
                    ""CreatedAtUtc"" timestamp with time zone NOT NULL DEFAULT now(),
                    ""UpdatedAtUtc"" timestamp with time zone NULL
                );
                CREATE INDEX IF NOT EXISTS ""IX_ApprovalRequests_Status""
                  ON identity.""ApprovalRequests"" (""Status"");
            ");

            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS identity.""RefreshTokens"" (
                    ""Id"" uuid PRIMARY KEY,
                    ""UserId"" uuid NOT NULL,
                    ""TokenHash"" varchar(64) NOT NULL,
                    ""ExpiresAtUtc"" timestamp with time zone NOT NULL,
                    ""RevokedAtUtc"" timestamp with time zone NULL,
                    ""ReplacedByTokenHash"" varchar(64) NULL,
                    ""CreatedAtUtc"" timestamp with time zone NOT NULL,
                    ""UpdatedAtUtc"" timestamp with time zone NULL,
                    CONSTRAINT ""FK_RefreshTokens_Persons_UserId""
                      FOREIGN KEY (""UserId"") REFERENCES identity.""Persons"" (""Id"") ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_RefreshTokens_TokenHash""
                  ON identity.""RefreshTokens"" (""TokenHash"");
                CREATE INDEX IF NOT EXISTS ""IX_RefreshTokens_UserId""
                  ON identity.""RefreshTokens"" (""UserId"");
            ");
        }
        await DataSeeder.SeedAsync(context, hasher, logger);
        if (context.Database.IsNpgsql())
        {
            await context.Database.ExecuteSqlRawAsync(@"
                UPDATE identity.""Employees"" e
                SET ""DepartmentRoleSlot"" = CASE
                    WHEN p.""IsActive"" = TRUE AND e.""Status"" = 'Active' AND e.""Role"" IN ('Admin', 'Manager')
                    THEN e.""Role"" || ':' || e.""DepartmentId""::text
                    ELSE NULL
                END
                FROM identity.""Persons"" p
                WHERE p.""Id"" = e.""Id"";

                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Employees_DepartmentRoleSlot""
                  ON identity.""Employees"" (""DepartmentRoleSlot"")
                  WHERE ""DepartmentRoleSlot"" IS NOT NULL;
            ");
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not complete database initialization on startup. Ensure PostgreSQL is accessible if running full persistence.");
    }
}

app.Run();

public partial class Program { }
