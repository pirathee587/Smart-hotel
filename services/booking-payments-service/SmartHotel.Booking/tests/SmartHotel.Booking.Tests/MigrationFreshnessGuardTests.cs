using FluentAssertions;
using Xunit;
using static SmartHotel.Booking.Tests.PostgresCheckoutConcurrencyTests;

namespace SmartHotel.Booking.Tests;

public class MigrationFreshnessGuardTests
{
    private const string InitialMigration = "20260918172955_InitialVersionedSchema";
    private const string Phase2Migration  = "20260922180000_AddCheckoutInitiatedAtUtcAndBackfill";

    [Fact]
    public void WhenHistoryAbsent_AndUserTablesExist_RejectsTargetDatabase()
    {
        // Arrange: __EFMigrationsHistory is absent, but user tables exist (e.g. from raw SQL or dump)
        var userTables = new[] { "Bookings", "Rooms" };

        // Act
        var result = MigrationFreshnessValidator.Evaluate(
            historyTableExists: false,
            appliedMigrations: Array.Empty<string>(),
            userTables: userTables,
            hasTableData: _ => false);

        // Assert
        result.IsFresh.Should().BeFalse();
        result.Reason.Should().Contain("__EFMigrationsHistory is absent");
        result.Reason.Should().Contain("Bookings, Rooms");
    }

    [Fact]
    public void WhenHistoryAbsent_AndNoUserTables_ReturnsFresh()
    {
        // Arrange: brand new database, zero tables
        var result = MigrationFreshnessValidator.Evaluate(
            historyTableExists: false,
            appliedMigrations: Array.Empty<string>(),
            userTables: Array.Empty<string>(),
            hasTableData: _ => false);

        // Assert
        result.IsFresh.Should().BeTrue();
        result.Reason.Should().BeNull();
    }

    [Fact]
    public void WhenHistoryExists_ButEmptyMigrations_RejectsAsAmbiguous()
    {
        // Arrange: history table created but 0 rows recorded
        var result = MigrationFreshnessValidator.Evaluate(
            historyTableExists: true,
            appliedMigrations: Array.Empty<string>(),
            userTables: Array.Empty<string>(),
            hasTableData: _ => false);

        // Assert
        result.IsFresh.Should().BeFalse();
        result.Reason.Should().Contain("contains no recorded migrations");
    }

    [Fact]
    public void WhenHistoryExists_WithPhase2OrLater_RejectsToPreventRollback()
    {
        // Arrange: Phase 2 migration already applied
        var applied = new[] { InitialMigration, Phase2Migration };

        var result = MigrationFreshnessValidator.Evaluate(
            historyTableExists: true,
            appliedMigrations: applied,
            userTables: new[] { "Bookings" },
            hasTableData: _ => false);

        // Assert
        result.IsFresh.Should().BeFalse();
        result.Reason.Should().Contain("already has Phase-2 (or later) migrations applied");
    }

    [Fact]
    public void WhenHistoryExists_WithUnknownMigration_RejectsTarget()
    {
        // Arrange: Migration history has unknown or custom migration
        var applied = new[] { "20250101000000_SomeOtherMigration" };

        var result = MigrationFreshnessValidator.Evaluate(
            historyTableExists: true,
            appliedMigrations: applied,
            userTables: new[] { "CustomTable" },
            hasTableData: _ => false);

        // Assert
        result.IsFresh.Should().BeFalse();
        result.Reason.Should().Contain("contains unexpected migrations");
    }

    [Fact]
    public void WhenHistoryExists_WithInitialMigration_AndApplicationDataExists_RejectsUsedDatabase()
    {
        // Arrange: Initial migration applied, but Bookings table has data
        var applied = new[] { InitialMigration };
        var userTables = new[] { "Bookings", "BookingDrafts" };

        var result = MigrationFreshnessValidator.Evaluate(
            historyTableExists: true,
            appliedMigrations: applied,
            userTables: userTables,
            hasTableData: table => table == "Bookings"); // Bookings has data

        // Assert
        result.IsFresh.Should().BeFalse();
        result.Reason.Should().Contain("Table 'Bookings' contains existing application data");
        result.Reason.Should().Contain("Do not roll back an already-used database");
    }

    [Fact]
    public void WhenHistoryExists_WithInitialMigration_AndZeroApplicationData_ReturnsFresh()
    {
        // Arrange: Initial migration applied, empty application tables
        var applied = new[] { InitialMigration };
        var userTables = new[] { "Bookings", "BookingDrafts" };

        var result = MigrationFreshnessValidator.Evaluate(
            historyTableExists: true,
            appliedMigrations: applied,
            userTables: userTables,
            hasTableData: _ => false); // All tables empty

        // Assert
        result.IsFresh.Should().BeTrue();
        result.Reason.Should().BeNull();
    }
}
