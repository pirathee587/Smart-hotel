using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SmartHotel.Notifications.Application;
using SmartHotel.Notifications.Domain;
using SmartHotel.Notifications.Infrastructure.Persistence;
using SmartHotel.Notifications.Infrastructure.Repositories;
using SmartHotel.Notifications.Infrastructure.Services;
using Xunit;

namespace SmartHotel.Notifications.Tests;

public class OfflineFallbackPersistenceTests
{
    private readonly NotificationsDbContext _dbContext;
    private readonly NotificationRepository _repository;
    private readonly Mock<ISignalRNotificationDispatcher> _dispatcherMock;
    private readonly Mock<ILogger<NotificationService>> _loggerMock;
    private readonly NotificationService _service;

    public OfflineFallbackPersistenceTests()
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new NotificationsDbContext(options);
        _repository = new NotificationRepository(_dbContext);
        _dispatcherMock = new Mock<ISignalRNotificationDispatcher>();
        _loggerMock = new Mock<ILogger<NotificationService>>();

        _service = new NotificationService(_repository, _dispatcherMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task SendNotification_AlwaysWritesToDatabase_WithUnreadStatus()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var result = await _service.SendNotificationAsync(
            userId,
            "Shift Started",
            "Your morning shift has been logged",
            NotificationType.System);

        // Assert: Written to DB
        result.Should().NotBeNull();
        result.IsRead.Should().BeFalse();
        result.Title.Should().Be("Shift Started");

        var stored = await _dbContext.Notifications.FirstOrDefaultAsync(n => n.Id == result.Id);
        stored.Should().NotBeNull();
        stored!.UserId.Should().Be(userId);
        stored.IsRead.Should().BeFalse();
        stored.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // SignalR was also triggered
        _dispatcherMock.Verify(d => d.SendNotificationToUserAsync(userId, It.IsAny<NotificationDto>(), default), Times.Once);
    }

    [Fact]
    public async Task SendNotification_WhenSignalRThrows_StillSafelyPersistsToDatabase()
    {
        // Arrange: Simulate disconnected WebSocket or SignalR error
        var userId = Guid.NewGuid();
        _dispatcherMock
            .Setup(d => d.SendNotificationToUserAsync(It.IsAny<Guid>(), It.IsAny<NotificationDto>(), default))
            .ThrowsAsync(new InvalidOperationException("No connection available for this user"));

        // Act: Must NOT throw; logs warning and returns DTO
        var result = await _service.SendNotificationAsync(
            userId,
            "Urgent Room Alert",
            "Water leakage reported in Room 402",
            NotificationType.RoomStatusUpdated);

        // Assert: Notification is safely stored in database for user to see on reconnect
        result.Should().NotBeNull();
        var stored = await _dbContext.Notifications.FirstOrDefaultAsync(n => n.Id == result.Id);
        stored.Should().NotBeNull();
        stored!.Title.Should().Be("Urgent Room Alert");
        stored.IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task GetUserNotifications_And_MarkAsRead_UpdatesDatabaseStatus()
    {
        // Arrange: Seed 2 notifications
        var userId = Guid.NewGuid();
        var n1 = await _service.SendNotificationAsync(userId, "Alert 1", "Message 1", NotificationType.System);
        var n2 = await _service.SendNotificationAsync(userId, "Alert 2", "Message 2", NotificationType.TaskAssigned);

        // Act 1: Initial unread count
        var initialCount = await _service.GetUnreadCountAsync(userId);
        initialCount.Should().Be(2);

        // Act 2: Mark one notification as read
        await _service.MarkAsReadAsync(n1.Id, userId);

        // Assert 1: Unread count drops to 1
        var updatedCount = await _service.GetUnreadCountAsync(userId);
        updatedCount.Should().Be(1);

        // Assert 2: n1 in DB is marked as read
        var readNotif = await _dbContext.Notifications.FindAsync(n1.Id);
        readNotif!.IsRead.Should().BeTrue();
        readNotif.ReadAt.Should().NotBeNull();

        // Act 3: Mark all as read
        await _service.MarkAllAsReadAsync(userId);
        var finalCount = await _service.GetUnreadCountAsync(userId);
        finalCount.Should().Be(0);
    }
}
