using System.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using SmartHotel.Booking.Application.Features.Bookings.Commands;
using SmartHotel.Booking.Application.Features.Bookings.DTOs;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Domain.Entities;
using SmartHotel.Booking.Domain.Enums;
using SmartHotel.Booking.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace SmartHotel.Booking.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// PostgreSQL Integration Test Harness — Phase 2  (NOT RUN until DB approved)
//
// ENVIRONMENT VARIABLES
//
// Tests 2-4 (conflict / concurrency / idempotency):
//   TEST_POSTGRES_CONNECTION         — Npgsql connection string to an isolated
//                                      test database (name must contain 'test',
//                                      must not be a known production database).
//   ALLOW_TEST_DB_MODIFICATIONS=true — explicit authorization flag.
//
// Test 1 (migration round-trip) uses a SEPARATE variable:
//   MIGRATION_TEST_POSTGRES_CONNECTION — Npgsql connection string to a FRESH,
//                                        dedicated migration-only database.
//                                        This database MUST be empty or contain
//                                        only InitialVersionedSchema. The test
//                                        will REFUSE to run against a database
//                                        that already has Phase-2 (or later)
//                                        migrations applied, because rolling
//                                        back an existing database is unsafe.
//   ALLOW_TEST_DB_MODIFICATIONS=true — same flag, required for both databases.
//
// WHY TWO DATABASES?
//   MigrateAsync("InitialVersionedSchema") is a DESTRUCTIVE rollback. Running
//   it on a database shared with the other three tests would leave the schema
//   without CheckoutInitiatedAtUtc between tests. A name containing 'test' and
//   ALLOW_TEST_DB_MODIFICATIONS=true are insufficient proof that the database
//   is fresh and disposable — a separately provisioned, empty database is the
//   only safe guarantee.
//
// ENGINE ATTESTATION:
//   Every test calls AssertRealPostgresEngineAsync() which opens a raw ADO.NET
//   connection (bypassing EF provider heuristics) and asserts SELECT version()
//   starts with "PostgreSQL". An InMemory/SQLite provider or a broken
//   connection will cause an explicit failure — never a silent pass.
// ─────────────────────────────────────────────────────────────────────────────

[Trait("Category", "PostgresIntegration")]
public class PostgresCheckoutConcurrencyTests
{
    private readonly ITestOutputHelper _output;

    public PostgresCheckoutConcurrencyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ── Stub payment gateway ─────────────────────────────────────────────────

    private sealed class TestPaymentGateway : IPaymentGateway
    {
        public PaymentExecutionOutcome Outcome { get; set; } = PaymentExecutionOutcome.Success;
        public string? ErrorMessage { get; set; }

        public Task<PaymentProcessResult> ProcessTokenizedPaymentAsync(
            string paymentToken,
            decimal amount,
            string currency,
            string bookingReference,
            CancellationToken ct = default)
            => Task.FromResult(new PaymentProcessResult(
                Outcome,
                $"TXN-{Guid.NewGuid():N}",
                ErrorMessage ?? string.Empty));
    }

    // ── Guard: validated Postgres options for Tests 2–4 (no fallbacks) ────────

    private static readonly string[] KnownProductionDatabases =
    [
        "smarthotel_bookings",
        "smarthotel_identity",
        "smarthotel_fieldops",
        "smarthotel_hotelops",
        "smarthotel_notifications",
    ];

    /// <summary>
    /// Validates a Npgsql connection string from the named environment variable.
    /// Rules enforced: variable must be present, database name must contain
    /// 'test', database must not match any known production name, and
    /// ALLOW_TEST_DB_MODIFICATIONS=true must be set.
    /// Never reads ConnectionStrings__DefaultConnection or any other fallback.
    /// </summary>
    private DbContextOptions<BookingDbContext> GetValidatedOptions(string envVarName)
    {
        var connectionString = Environment.GetEnvironmentVariable(envVarName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Fail(
                $"NOT RUN: {envVarName} environment variable is not set. " +
                "Live PostgreSQL integration tests require an explicit isolated " +
                "test database connection string. This test never falls back to " +
                "ConnectionStrings__DefaultConnection or any other source.");
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString!);
        var dbName  = (builder.Database ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(dbName) ||
            !dbName.Contains("test", StringComparison.OrdinalIgnoreCase) ||
            Array.Exists(KnownProductionDatabases,
                p => dbName.Equals(p, StringComparison.OrdinalIgnoreCase)))
        {
            Assert.Fail(
                $"REJECTED UNAPPROVED TARGET ({envVarName}): Target database " +
                $"'{dbName}' is not an authorized test database. The name must " +
                "contain the word 'test' (e.g. smarthotel_bookings_test) and must " +
                "not match any known production or main development database.");
        }

        var user = (builder.Username ?? string.Empty).Trim();
        if (string.Equals(user, "postgres", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Fail(
                $"REJECTED SUPERUSER TARGET ({envVarName}): Target connection uses " +
                "the 'postgres' superuser. Tests must run under a dedicated, restricted " +
                "non-superuser test role (e.g. smarthotel_test_runner) to prevent " +
                "accidental cross-database access.");
        }

        var allowMod = Environment.GetEnvironmentVariable("ALLOW_TEST_DB_MODIFICATIONS");
        if (!string.Equals(allowMod, "true", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Fail(
                "UNAPPROVED EXECUTION: ALLOW_TEST_DB_MODIFICATIONS=true must be " +
                "explicitly set to authorize schema migration and destructive test " +
                "execution on the target database.");
        }

        return new DbContextOptionsBuilder<BookingDbContext>()
            .UseNpgsql(connectionString!)
            .Options;
    }

    /// <summary>Used by Tests 2–4 (conflict, concurrency, idempotency).</summary>
    private DbContextOptions<BookingDbContext> GetValidatedPostgresOptions()
        => GetValidatedOptions("TEST_POSTGRES_CONNECTION");

    /// <summary>
    /// Used exclusively by the migration round-trip test (Test 1).
    /// Requires MIGRATION_TEST_POSTGRES_CONNECTION pointing to a fresh,
    /// dedicated migration-only database — never TEST_POSTGRES_CONNECTION.
    /// </summary>
    private DbContextOptions<BookingDbContext> GetValidatedMigrationTestOptions()
        => GetValidatedOptions("MIGRATION_TEST_POSTGRES_CONNECTION");

    // ── Engine attestation (raw ADO.NET — bypasses EF provider heuristics) ──

    /// <summary>
    /// Opens a raw ADO.NET connection and executes SELECT version() to verify
    /// the engine is genuinely PostgreSQL. An InMemory provider, a closed
    /// connection, or a non-PostgreSQL engine will cause an explicit failure.
    /// </summary>
    private async Task AssertRealPostgresEngineAsync(BookingDbContext context)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT version();";
        var result = await cmd.ExecuteScalarAsync();

        var versionString = result?.ToString() ?? string.Empty;
        _output.WriteLine($"[ENGINE] {versionString}");

        versionString.Should().StartWith("PostgreSQL",
            "Integration test must connect to a real PostgreSQL server. " +
            "An InMemory or SQLite provider must never silently pass this check.");
    }

    // ── Migration DB freshness assertion & validator ─────────────────────────

    public record FreshnessEvaluationResult(bool IsFresh, string? Reason);

    /// <summary>
    /// Pure validation logic for migration test database freshness.
    /// Evaluated against database metadata to guarantee:
    ///   - If __EFMigrationsHistory is absent, public schema must have ZERO user tables.
    ///   - If __EFMigrationsHistory exists, it must contain ONLY InitialVersionedSchema,
    ///     and all existing application tables must contain ZERO rows.
    ///   - An already-used database is never rolled back.
    /// </summary>
    public static class MigrationFreshnessValidator
    {
        public const string InitialMigration = "20260918172955_InitialVersionedSchema";
        public const string Phase2Migration  = "20260922180000_AddCheckoutInitiatedAtUtcAndBackfill";

        public static FreshnessEvaluationResult Evaluate(
            bool historyTableExists,
            IReadOnlyList<string> appliedMigrations,
            IReadOnlyList<string> userTables,
            Func<string, bool> hasTableData)
        {
            if (!historyTableExists)
            {
                if (userTables.Count > 0)
                {
                    return new FreshnessEvaluationResult(
                        false,
                        $"__EFMigrationsHistory is absent, but the target schema contains {userTables.Count} " +
                        $"pre-existing table(s): [{string.Join(", ", userTables)}]. " +
                        "The target database is not empty and cannot be proven fresh. " +
                        "Refusing to execute migration test against a non-empty database.");
                }

                return new FreshnessEvaluationResult(true, null);
            }

            if (appliedMigrations.Count == 0)
            {
                return new FreshnessEvaluationResult(
                    false,
                    "__EFMigrationsHistory table exists but contains no recorded migrations. Database state is ambiguous.");
            }

            var hasPhase2OrLater = appliedMigrations.Any(m =>
                string.Compare(m, Phase2Migration, StringComparison.Ordinal) >= 0);

            if (hasPhase2OrLater)
            {
                return new FreshnessEvaluationResult(
                    false,
                    "The database already has Phase-2 (or later) migrations applied: " +
                    $"[{string.Join(", ", appliedMigrations)}]. Rolling back an existing database is unsafe. " +
                    "Provision a fresh, empty database.");
            }

            var unknownMigrations = appliedMigrations.Where(m => m != InitialMigration).ToList();
            if (unknownMigrations.Count > 0)
            {
                return new FreshnessEvaluationResult(
                    false,
                    $"The database contains unexpected migrations: [{string.Join(", ", unknownMigrations)}]. " +
                    $"Only '{InitialMigration}' is an acceptable pre-existing migration.");
            }

            foreach (var table in userTables)
            {
                if (hasTableData(table))
                {
                    return new FreshnessEvaluationResult(
                        false,
                        $"Table '{table}' contains existing application data. " +
                        "The migration test requires a fresh database with no application data. " +
                        "Do not roll back an already-used database.");
                }
            }

            return new FreshnessEvaluationResult(true, null);
        }
    }

    /// <summary>
    /// Reads catalog metadata and asserts the database is in a known-safe,
    /// fresh state for the migration round-trip test.
    /// Fails closed if freshness cannot be unequivocally established.
    /// </summary>
    private async Task AssertMigrationDatabaseIsFreshAsync(BookingDbContext context)
    {
        try
        {
            var conn = context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            // 1. Check whether __EFMigrationsHistory exists.
            await using var existsCmd = conn.CreateCommand();
            existsCmd.CommandText = @"
                SELECT COUNT(*)::int
                FROM   information_schema.tables
                WHERE  table_schema = 'public'
                  AND  table_name   = '__EFMigrationsHistory';";
            var historyTableExists = Convert.ToInt32(await existsCmd.ExecuteScalarAsync()) == 1;

            // 2. Discover all user-created base tables in public (excluding __EFMigrationsHistory).
            await using var tablesCmd = conn.CreateCommand();
            tablesCmd.CommandText = @"
                SELECT table_name
                FROM   information_schema.tables
                WHERE  table_schema = 'public'
                  AND  table_type   = 'BASE TABLE'
                  AND  table_name  != '__EFMigrationsHistory'
                ORDER BY table_name;";
            var userTables = new List<string>();
            await using (var reader = await tablesCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    userTables.Add(reader.GetString(0));
            }

            // 3. Read applied migration IDs if __EFMigrationsHistory exists.
            var appliedMigrations = new List<string>();
            if (historyTableExists)
            {
                await using var rowsCmd = conn.CreateCommand();
                rowsCmd.CommandText = "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\";";
                await using var reader = await rowsCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    appliedMigrations.Add(reader.GetString(0));

                _output.WriteLine(
                    $"[FRESHNESS] Applied migrations: [{string.Join(", ", appliedMigrations)}]");
            }

            // 4. Delegate to validator with raw ADO.NET data probe
            bool HasTableData(string tableName)
            {
                using var checkCmd = conn.CreateCommand();
                var quoted = "\"" + tableName.Replace("\"", "\"\"") + "\"";
                checkCmd.CommandText = $"SELECT 1 FROM {quoted} LIMIT 1;";
                return checkCmd.ExecuteScalar() is not null;
            }

            var evaluation = MigrationFreshnessValidator.Evaluate(
                historyTableExists,
                appliedMigrations,
                userTables,
                HasTableData);

            if (!evaluation.IsFresh)
            {
                Assert.Fail($"MIGRATION TEST ABORTED: {evaluation.Reason}");
            }

            _output.WriteLine(historyTableExists
                ? "[FRESHNESS] Database is safe (only InitialVersionedSchema, zero application rows)."
                : "[FRESHNESS] Database is completely fresh (zero tables). Safe to proceed.");
        }
        catch (Exception ex) when (ex is not Xunit.Sdk.XunitException)
        {
            Assert.Fail(
                $"MIGRATION TEST ABORTED (fail-closed): Could not establish database freshness: " +
                $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // TEST 1: Migration round-trip — column, partial index, and backfill
    //
    // REQUIRES: MIGRATION_TEST_POSTGRES_CONNECTION pointing to a fresh,
    //           dedicated, disposable database that does NOT already have
    //           Phase-2 (or later) migrations applied.
    //           DO NOT point this at TEST_POSTGRES_CONNECTION.
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates the Phase 2 migration on real PostgreSQL.
    /// Uses MIGRATION_TEST_POSTGRES_CONNECTION — a SEPARATE fresh database.
    ///
    /// Safety preconditions enforced before any schema changes:
    ///   - Engine is real PostgreSQL (ADO.NET version check).
    ///   - Database contains no Phase-2 or later migrations (__EFMigrationsHistory).
    ///
    /// Steps:
    ///   1. Migrate to InitialVersionedSchema (no-op if already there; rolls
    ///      back only if the DB is at an earlier point, which freshness check
    ///      already validated is safe).
    ///   2. Seed a legacy PendingPayment row without CheckoutInitiatedAtUtc.
    ///   3. Apply AddCheckoutInitiatedAtUtcAndBackfill.
    ///   4. Assert column in information_schema.
    ///   5. Assert partial index in pg_indexes.
    ///   6. Assert legacy row backfilled (CheckoutInitiatedAtUtc = CreatedAtUtc).
    ///
    /// Cleanup (always in finally):
    ///   Migrates to latest. If this FAILS the test asserts failure —
    ///   the database state is unknown and must be inspected manually.
    /// </summary>
    [Fact]
    public async Task Postgres_Migration_AddCheckoutInitiatedAtUtc_BackfillsAndIndexesCorrectly()
    {
        // Uses the dedicated migration DB, never TEST_POSTGRES_CONNECTION.
        var options = GetValidatedMigrationTestOptions();
        await using var context = new BookingDbContext(options);
        await AssertRealPostgresEngineAsync(context);

        // PRE-CONDITION: verify the database is in a known-safe state.
        // This check will Assert.Fail if Phase-2 or any unknown migration is
        // already applied, preventing an unsafe rollback of an existing database.
        await AssertMigrationDatabaseIsFreshAsync(context);

        var migrator = context.GetService<IMigrator>();
        Exception? cleanupException = null;

        try
        {
            // STEP 1 — Migrate to InitialVersionedSchema.
            //   - Fresh DB (no migrations): applies InitialVersionedSchema forward.
            //   - DB already at InitialVersionedSchema: no-op.
            //   Both are safe because freshness check already excluded Phase-2+.
            _output.WriteLine("[STEP 1] Migrating to InitialVersionedSchema...");
            await migrator.MigrateAsync("20260918172955_InitialVersionedSchema");

            // STEP 2 — Seed a legacy PendingPayment row via raw SQL.
            //           CheckoutInitiatedAtUtc column does not exist yet.
            _output.WriteLine("[STEP 2] Seeding legacy PendingPayment row...");
            var legacyBookingId  = Guid.NewGuid();
            var legacyRoomId     = Guid.NewGuid();
            var legacyRoomTypeId = Guid.NewGuid();
            var legacyCreatedAt  = DateTime.UtcNow.AddDays(-2);
            // Fixed 20-char reference, safe for any column length constraint.
            var legacyRef = $"TH-LEGACY-{legacyBookingId:N}"[..20];

            await context.Database.ExecuteSqlRawAsync(@"
                INSERT INTO ""Bookings"" (
                    ""Id"", ""BookingReference"", ""CustomerId"",
                    ""CustomerLastName"", ""CustomerEmail"",
                    ""RoomId"", ""RoomNumber"", ""RoomTypeId"",
                    ""CheckInDate"", ""CheckOutDate"",
                    ""GuestCount"", ""TotalAmount"", ""Status"", ""CreatedAtUtc""
                ) VALUES (
                    {0}, {1}, {2},
                    'LegacyGuest', 'legacy@example.test',
                    {3}, '101', {4},
                    '2026-11-01', '2026-11-05',
                    2, 500.00, 0, {5}
                );",
                legacyBookingId,
                legacyRef,
                Guid.NewGuid(),
                legacyRoomId,
                legacyRoomTypeId,
                legacyCreatedAt);

            // STEP 3 — Apply the Phase 2 migration.
            _output.WriteLine("[STEP 3] Applying AddCheckoutInitiatedAtUtcAndBackfill...");
            await migrator.MigrateAsync("20260922180000_AddCheckoutInitiatedAtUtcAndBackfill");

            // STEP 4 — Verify column in information_schema.
            _output.WriteLine("[STEP 4] Verifying CheckoutInitiatedAtUtc column...");
            int columnCount;
            {
                var conn = context.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open) await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT COUNT(*)::int
                    FROM information_schema.columns
                    WHERE table_name   = 'Bookings'
                      AND column_name  = 'CheckoutInitiatedAtUtc'
                      AND data_type    = 'timestamp with time zone';";
                columnCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }
            columnCount.Should().Be(1,
                "information_schema must report CheckoutInitiatedAtUtc as " +
                "'timestamp with time zone' after applying the Phase 2 migration");

            // STEP 5 — Verify partial index in pg_indexes.
            _output.WriteLine("[STEP 5] Verifying partial index...");
            string? indexDef;
            {
                var conn = context.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open) await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT indexdef
                    FROM pg_indexes
                    WHERE tablename = 'Bookings'
                      AND indexname = 'IX_Bookings_PendingPayment_CheckoutInitiated';";
                indexDef = (await cmd.ExecuteScalarAsync())?.ToString();
            }
            indexDef.Should().NotBeNull(
                "Partial index IX_Bookings_PendingPayment_CheckoutInitiated must " +
                "exist in pg_indexes after migration");
            indexDef!.Should().Contain("\"Status\" = 0",
                "Partial index filter must target Status = 0 (PendingPayment)");

            // STEP 6 — Verify atomic backfill of the legacy row.
            _output.WriteLine("[STEP 6] Verifying legacy row backfill...");
            DateTime? backfilledTs;
            {
                var conn = context.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open) await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT ""CheckoutInitiatedAtUtc""
                    FROM   ""Bookings""
                    WHERE  ""Id"" = @id;";
                var param = cmd.CreateParameter();
                param.ParameterName = "id";
                param.Value = legacyBookingId;
                cmd.Parameters.Add(param);
                var raw = await cmd.ExecuteScalarAsync();
                backfilledTs = (raw is null || raw == DBNull.Value)
                    ? (DateTime?)null
                    : Convert.ToDateTime(raw);
            }
            backfilledTs.Should().NotBeNull(
                "Legacy PendingPayment row must be backfilled with a non-null " +
                "CheckoutInitiatedAtUtc by the Phase 2 migration");
            backfilledTs!.Value.Should().BeCloseTo(
                legacyCreatedAt,
                precision: TimeSpan.FromSeconds(2),
                because: "Backfill must set CheckoutInitiatedAtUtc = CreatedAtUtc " +
                         "(± 2 s UTC normalization tolerance)");

            _output.WriteLine("[PASS] Migration, column, partial index, and backfill verified.");
        }
        finally
        {
            // Migrate to latest to leave the database in a consistent state.
            // If this fails the database schema is unknown — we must FAIL the
            // test explicitly so an operator inspects and repairs the DB before
            // re-running. A silent warning here would hide a broken test database.
            _output.WriteLine("[CLEANUP] Migrating to latest...");
            try
            {
                await migrator.MigrateAsync();
                _output.WriteLine("[CLEANUP] Migrate-to-latest succeeded.");
            }
            catch (Exception ex)
            {
                cleanupException = ex;
                _output.WriteLine(
                    $"[CLEANUP FAILED] migrate-to-latest threw: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // Assert cleanup success OUTSIDE the finally-block so the assertion
        // exception is not swallowed by the finally handler.
        if (cleanupException is not null)
        {
            Assert.Fail(
                "MIGRATION TEST CLEANUP FAILED: migrate-to-latest threw an exception " +
                "after the test completed. The migration test database is now in an " +
                "UNKNOWN state and must be inspected and repaired before re-running. " +
                $"Exception: {cleanupException.GetType().Name}: {cleanupException.Message}");
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // TEST 2: Staff booking (CheckoutInitiatedAtUtc = null, >15 min old) AND
    //         unknown-outcome hold (CheckoutInitiatedAtUtc set, >60 min old)
    //         both block overlapping guest draft creation on real PostgreSQL.
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Postgres_AvailabilityConflicts_OldStaffAndUnknownHolds_BlockOverlappingBookings()
    {
        var options = GetValidatedPostgresOptions();
        await using var context = new BookingDbContext(options);
        await AssertRealPostgresEngineAsync(context);

        var roomTypeId = Guid.NewGuid();
        var checkIn   = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var checkOut  = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(13));

        // ── Part A: staff PendingPayment (CheckoutInitiatedAtUtc = null, 35 min old)
        //
        // IMPORTANT: FakeHotelOpsClient must have Room + RoomType configured.
        // Without them, CreateDraftCommandHandler returns "room does not exist"
        // before reaching the availability check — the test would pass for the
        // wrong reason and would not actually verify the conflict rule.
        var staffRoomId = Guid.NewGuid();
        var fakeClientStaff = new FakeHotelOpsClient
        {
            RoomType = new RoomTypeInfo(
                Id:           roomTypeId,
                Name:         "Staff Test Room",
                PricePerNight: 200m,
                CleaningFee:  0m,
                AmenitiesFee: 0m,
                Capacity:     4,
                IsPublished:  true,
                IsActive:     true,
                Currency:     "LKR"),
            Room = new RoomInfo(
                Id:         staffRoomId,
                RoomTypeId: roomTypeId,
                RoomNumber: "301",
                Floor:      3,
                Status:     "Available")
        };

        context.Bookings.Add(new Domain.Entities.Booking
        {
            Id                   = Guid.NewGuid(),
            BookingReference     = Domain.Entities.Booking.GenerateBookingReference(),
            CustomerId           = Guid.NewGuid(),
            CustomerLastName     = "StaffGuest",
            CustomerEmail        = "staff@example.test",
            RoomId               = staffRoomId,
            RoomNumber           = "301",
            RoomTypeId           = roomTypeId,
            CheckInDate          = checkIn,
            CheckOutDate         = checkOut,
            GuestCount           = 2,
            TotalAmount          = 400m,
            Status               = BookingStatus.PendingPayment,
            CheckoutInitiatedAtUtc = null,                          // Staff booking
            CreatedAtUtc         = DateTime.UtcNow.AddMinutes(-35),
            UpdatedAtUtc         = DateTime.UtcNow.AddMinutes(-35)
        });
        await context.SaveChangesAsync();

        var draftHandlerStaff = new CreateDraftCommandHandler(context, fakeClientStaff);
        var staffResult = await draftHandlerStaff.Handle(new CreateDraftCommand(
            CustomerId:   Guid.NewGuid(),
            CustomerEmail: "guest@example.test",
            RoomId:       staffRoomId,
            RoomName:     "Staff Test Room",
            RoomTypeId:   roomTypeId,
            RatePlanId:   "STD",
            RatePlanName: "Standard",
            CheckInDate:  checkIn,
            CheckOutDate: checkOut,
            GuestCount:   2,
            RoomsCount:   1,
            PricePerNight: 200m),
            CancellationToken.None);

        staffResult.Succeeded.Should().BeFalse(
            "Staff PendingPayment booking (CheckoutInitiatedAtUtc = null, 35 min old) " +
            "MUST block guest draft on real PostgreSQL — no age-based exemption for staff bookings");
        staffResult.Message.Should().Contain("not available",
            "Conflict message must state the room is not available");
        _output.WriteLine("[PASS A] Staff booking (null CIOUtc, 35 min) blocks guest draft.");

        // ── Part B: unknown-outcome hold (CheckoutInitiatedAtUtc set, 75 min old)
        var unknownRoomId = Guid.NewGuid();
        var fakeClientUnknown = new FakeHotelOpsClient
        {
            RoomType = new RoomTypeInfo(
                Id:           roomTypeId,
                Name:         "Unknown Hold Room",
                PricePerNight: 200m,
                CleaningFee:  0m,
                AmenitiesFee: 0m,
                Capacity:     4,
                IsPublished:  true,
                IsActive:     true,
                Currency:     "LKR"),
            Room = new RoomInfo(
                Id:         unknownRoomId,
                RoomTypeId: roomTypeId,
                RoomNumber: "302",
                Floor:      3,
                Status:     "Available")
        };

        context.Bookings.Add(new Domain.Entities.Booking
        {
            Id                     = Guid.NewGuid(),
            BookingReference       = Domain.Entities.Booking.GenerateBookingReference(),
            CustomerId             = Guid.NewGuid(),
            CustomerLastName       = "UnknownGuest",
            CustomerEmail          = "unknown@example.test",
            RoomId                 = unknownRoomId,
            RoomNumber             = "302",
            RoomTypeId             = roomTypeId,
            CheckInDate            = checkIn,
            CheckOutDate           = checkOut,
            GuestCount             = 2,
            TotalAmount            = 400m,
            Status                 = BookingStatus.PendingPayment,
            CheckoutInitiatedAtUtc = DateTime.UtcNow.AddMinutes(-75), // Real checkout attempt
            CreatedAtUtc           = DateTime.UtcNow.AddMinutes(-75),
            UpdatedAtUtc           = DateTime.UtcNow.AddMinutes(-75)
        });
        await context.SaveChangesAsync();

        var draftHandlerUnknown = new CreateDraftCommandHandler(context, fakeClientUnknown);
        var unknownResult = await draftHandlerUnknown.Handle(new CreateDraftCommand(
            CustomerId:   Guid.NewGuid(),
            CustomerEmail: "guest2@example.test",
            RoomId:       unknownRoomId,
            RoomName:     "Unknown Hold Room",
            RoomTypeId:   roomTypeId,
            RatePlanId:   "STD",
            RatePlanName: "Standard",
            CheckInDate:  checkIn,
            CheckOutDate: checkOut,
            GuestCount:   2,
            RoomsCount:   1,
            PricePerNight: 200m),
            CancellationToken.None);

        unknownResult.Succeeded.Should().BeFalse(
            "Unknown-outcome hold (CheckoutInitiatedAtUtc set, 75 min old) MUST block " +
            "guest draft on real PostgreSQL — elapsed time must never release an " +
            "unresolved payment hold");
        unknownResult.Message.Should().Contain("not available",
            "Conflict message must state the room is not available");
        _output.WriteLine("[PASS B] Unknown-outcome hold (75 min) blocks guest draft.");
    }

    // ────────────────────────────────────────────────────────────────────────
    // TEST 3: Two DISTINCT drafts for the same room — concurrent checkout.
    //         Exactly ONE must succeed; the other must get an advisory-lock
    //         conflict ("already booked").
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Postgres_ConcurrentCheckouts_TwoDistinctDrafts_ExactlyOneSucceeds()
    {
        var options    = GetValidatedPostgresOptions();
        await using var setupCtx = new BookingDbContext(options);
        await AssertRealPostgresEngineAsync(setupCtx);

        var roomId     = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var guest1Id   = Guid.NewGuid();
        var guest2Id   = Guid.NewGuid();

        // Shared, stateless fake client — both handlers will see the same room.
        var fakeClient = new FakeHotelOpsClient
        {
            RoomType = new RoomTypeInfo(
                Id:           roomTypeId,
                Name:         "Deluxe Ocean Suite",
                PricePerNight: 200m,
                CleaningFee:  0m,
                AmenitiesFee: 0m,
                Capacity:     2,
                IsPublished:  true,
                IsActive:     true),
            Room = new RoomInfo(
                Id:         roomId,
                RoomTypeId: roomTypeId,
                RoomNumber: "401",
                Floor:      4,
                Status:     "Available")
        };

        var checkIn  = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(40));
        var checkOut = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(43));

        // Two DISTINCT drafts — different guests, same physical room + dates.
        var draft1 = new BookingDraft
        {
            Id            = Guid.NewGuid(),
            CustomerId    = guest1Id,
            CustomerEmail = "guest1@example.test",
            RoomId        = roomId,
            RoomName      = "Deluxe Ocean Suite",
            RoomTypeId    = roomTypeId,
            RatePlanId    = "STD",
            RatePlanName  = "Standard",
            CheckInDate   = checkIn,
            CheckOutDate  = checkOut,
            GuestCount    = 2,
            RoomsCount    = 1,
            PricePerNight = 200m,
            Currency      = "LKR",
            Status        = "Draft"
        };
        draft1.RecalculateTotals();

        var draft2 = new BookingDraft
        {
            Id            = Guid.NewGuid(),
            CustomerId    = guest2Id,
            CustomerEmail = "guest2@example.test",
            RoomId        = roomId,
            RoomName      = "Deluxe Ocean Suite",
            RoomTypeId    = roomTypeId,
            RatePlanId    = "STD",
            RatePlanName  = "Standard",
            CheckInDate   = checkIn,
            CheckOutDate  = checkOut,
            GuestCount    = 2,
            RoomsCount    = 1,
            PricePerNight = 200m,
            Currency      = "LKR",
            Status        = "Draft"
        };
        draft2.RecalculateTotals();

        setupCtx.BookingDrafts.AddRange(draft1, draft2);
        await setupCtx.SaveChangesAsync();

        // Two SEPARATE DbContexts so each gets its own database connection.
        // Sharing a single DbContext would serialize writes automatically and
        // would not exercise the PostgreSQL advisory lock at all.
        await using var ctx1 = new BookingDbContext(options);
        await using var ctx2 = new BookingDbContext(options);
        var gateway = new TestPaymentGateway();

        var h1 = new CheckoutCommandHandler(ctx1, gateway, fakeClient);
        var h2 = new CheckoutCommandHandler(ctx2, gateway, fakeClient);

        var req1 = new CheckoutRequest(
            DraftId:         draft1.Id,
            Contact:         new ContactInfoDto("Guest", "One", "+15550001", "guest1@example.test"),
            Address:         null, SpecialRequests: null, Loyalty: null,
            PaymentToken:    "tok_visa_1", CouponCode: null);

        var req2 = new CheckoutRequest(
            DraftId:         draft2.Id,
            Contact:         new ContactInfoDto("Guest", "Two", "+15550002", "guest2@example.test"),
            Address:         null, SpecialRequests: null, Loyalty: null,
            PaymentToken:    "tok_visa_2", CouponCode: null);

        var results = await Task.WhenAll(
            h1.Handle(new CheckoutCommand(req1, guest1Id), CancellationToken.None),
            h2.Handle(new CheckoutCommand(req2, guest2Id), CancellationToken.None));

        var successCount  = results.Count(r => r.Succeeded);
        var conflictCount = results.Count(r => !r.Succeeded && r.Message.Contains("already booked"));

        _output.WriteLine(
            $"[RESULT] Two distinct drafts: Successes={successCount}, Conflicts={conflictCount}");
        foreach (var r in results)
            _output.WriteLine($"  -> Succeeded={r.Succeeded} | {r.Message}");

        successCount.Should().Be(1,
            "pg_advisory_xact_lock must allow exactly ONE of the two distinct drafts " +
            "to reserve the same room");
        conflictCount.Should().Be(1,
            "The losing draft must be rejected with an 'already booked' conflict");
    }

    // ────────────────────────────────────────────────────────────────────────
    // TEST 4: Same draft submitted concurrently — idempotent single execution.
    //         (kept distinct from the two-drafts scenario above)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Postgres_ConcurrentCheckouts_SameDraft_IdempotentSingleExecution()
    {
        var options    = GetValidatedPostgresOptions();
        await using var setupCtx = new BookingDbContext(options);
        await AssertRealPostgresEngineAsync(setupCtx);

        var roomId     = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var guestId    = Guid.NewGuid();

        var fakeClient = new FakeHotelOpsClient
        {
            RoomType = new RoomTypeInfo(
                Id:           roomTypeId,
                Name:         "Deluxe Ocean Suite",
                PricePerNight: 200m,
                CleaningFee:  0m,
                AmenitiesFee: 0m,
                Capacity:     2,
                IsPublished:  true,
                IsActive:     true),
            Room = new RoomInfo(
                Id:         roomId,
                RoomTypeId: roomTypeId,
                RoomNumber: "402",
                Floor:      4,
                Status:     "Available")
        };

        var checkIn  = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(50));
        var checkOut = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(53));

        // Single draft submitted concurrently (e.g. double-tap on mobile).
        var draft = new BookingDraft
        {
            Id            = Guid.NewGuid(),
            CustomerId    = guestId,
            CustomerEmail = "guest@example.test",
            RoomId        = roomId,
            RoomName      = "Deluxe Ocean Suite",
            RoomTypeId    = roomTypeId,
            RatePlanId    = "STD",
            RatePlanName  = "Standard",
            CheckInDate   = checkIn,
            CheckOutDate  = checkOut,
            GuestCount    = 2,
            RoomsCount    = 1,
            PricePerNight = 200m,
            Currency      = "LKR",
            Status        = "Draft"
        };
        draft.RecalculateTotals();
        setupCtx.BookingDrafts.Add(draft);
        await setupCtx.SaveChangesAsync();

        // Separate DbContext instances = separate DB connections = real lock contention.
        await using var ctx1 = new BookingDbContext(options);
        await using var ctx2 = new BookingDbContext(options);
        var gateway = new TestPaymentGateway();

        var h1 = new CheckoutCommandHandler(ctx1, gateway, fakeClient);
        var h2 = new CheckoutCommandHandler(ctx2, gateway, fakeClient);

        var req = new CheckoutRequest(
            DraftId:      draft.Id,
            Contact:      new ContactInfoDto("Guest", "SameDraft", "+15550003", "guest@example.test"),
            Address:      null, SpecialRequests: null, Loyalty: null,
            PaymentToken: "tok_visa_same", CouponCode: null);

        var results = await Task.WhenAll(
            h1.Handle(new CheckoutCommand(req, guestId), CancellationToken.None),
            h2.Handle(new CheckoutCommand(req, guestId), CancellationToken.None));

        var successCount    = results.Count(r => r.Succeeded);
        var inProgressCount = results.Count(r =>
            !r.Succeeded && r.Message.Contains("already in progress"));

        _output.WriteLine(
            $"[RESULT] Same draft: Successes={successCount}, InProgress={inProgressCount}");
        foreach (var r in results)
            _output.WriteLine($"  -> Succeeded={r.Succeeded} | {r.Message}");

        successCount.Should().Be(1,
            "Exactly one concurrent request for the same draft must proceed to checkout");
        inProgressCount.Should().Be(1,
            "The duplicate concurrent request must be rejected as 'already in progress' " +
            "without double-charging the guest");
    }
}
