using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using SmartHotel.Notifications.Application;
using SmartHotel.Notifications.Domain;
using SmartHotel.Notifications.Infrastructure.Messaging;
using Xunit;

namespace SmartHotel.Notifications.Tests;

public class RabbitMQConsumerRoutingTests
{
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IServiceScope> _scopeMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<ISignalRNotificationDispatcher> _dispatcherMock;
    private readonly Mock<ILogger<NotificationEventConsumer>> _loggerMock;
    private readonly IConfiguration _configuration;
    private readonly NotificationEventConsumer _consumer;

    public RabbitMQConsumerRoutingTests()
    {
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeMock = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _notificationServiceMock = new Mock<INotificationService>();
        _dispatcherMock = new Mock<ISignalRNotificationDispatcher>();
        _loggerMock = new Mock<ILogger<NotificationEventConsumer>>();

        var configValues = new Dictionary<string, string?>
        {
            ["RabbitMQ:Host"] = "localhost",
            ["RabbitMQ:Port"] = "5672"
        };
        _configuration = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();

        _scopeFactoryMock.Setup(s => s.CreateScope()).Returns(_scopeMock.Object);
        _scopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);

        _serviceProviderMock
            .Setup(p => p.GetService(typeof(INotificationService)))
            .Returns(_notificationServiceMock.Object);
        _serviceProviderMock
            .Setup(p => p.GetService(typeof(ISignalRNotificationDispatcher)))
            .Returns(_dispatcherMock.Object);

        _consumer = new NotificationEventConsumer(_scopeFactoryMock.Object, _configuration, _loggerMock.Object);
    }

    [Fact]
    public async Task ProcessEventAsync_TaskDispatched_SavesNotificationAndDispatchesSignalR()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new
        {
            taskId = Guid.NewGuid(),
            title = "Clean Room 302",
            assignedEmployeeId = employeeId.ToString(),
            priority = "High"
        });

        _notificationServiceMock
            .Setup(s => s.SendNotificationAsync(
                employeeId,
                It.IsAny<string>(),
                It.Is<string>(m => m.Contains("Clean Room 302")),
                NotificationType.TaskAssigned,
                payload,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationDto(
                Guid.NewGuid(), employeeId, "New Task Assigned", "Clean Room 302",
                NotificationType.TaskAssigned, payload, false, DateTime.UtcNow, null));

        // Act
        await _consumer.ProcessEventAsync("task.dispatched", payload);

        // Assert
        _notificationServiceMock.Verify(s => s.SendNotificationAsync(
            employeeId,
            "New Task Assigned",
            It.Is<string>(m => m.Contains("Clean Room 302") && m.Contains("High")),
            NotificationType.TaskAssigned,
            payload,
            It.IsAny<CancellationToken>()), Times.Once);

        _dispatcherMock.Verify(d => d.BroadcastTaskAssignedAsync(
            employeeId,
            It.IsAny<JsonElement>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessEventAsync_RoomStatusChanged_DispatchesToStaffRoles()
    {
        // Arrange
        var payload = JsonSerializer.Serialize(new
        {
            roomId = Guid.NewGuid(),
            roomNumber = "204",
            previousStatus = "Occupied",
            newStatus = "Dirty"
        });

        // Act
        await _consumer.ProcessEventAsync("room.status_changed", payload);

        // Assert
        _dispatcherMock.Verify(d => d.BroadcastRoomStatusUpdatedAsync(
            It.Is<string[]>(roles => roles.Contains("role_Housekeeper") && roles.Contains("role_Manager")),
            It.IsAny<JsonElement>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessEventAsync_KdsStatusChanged_WhenReady_DispatchesDeliveryAlert()
    {
        // Arrange
        var payload = JsonSerializer.Serialize(new
        {
            orderId = Guid.NewGuid(),
            tableOrRoom = "Room 105",
            status = "Ready",
            items = new[] { "Club Sandwich", "Iced Tea" }
        });

        // Act
        await _consumer.ProcessEventAsync("kds.status_changed", payload);

        // Assert
        _dispatcherMock.Verify(d => d.BroadcastDeliveryAlertAsync(
            It.Is<string[]>(roles => roles.Contains("role_Waiter") && roles.Contains("role_Manager")),
            It.IsAny<JsonElement>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessEventAsync_KdsStatusChanged_WhenInProgress_DoesNotDispatchAlert()
    {
        // Arrange
        var payload = JsonSerializer.Serialize(new
        {
            orderId = Guid.NewGuid(),
            status = "InProgress"
        });

        // Act
        await _consumer.ProcessEventAsync("kds.status_changed", payload);

        // Assert
        _dispatcherMock.Verify(d => d.BroadcastDeliveryAlertAsync(
            It.IsAny<string[]>(),
            It.IsAny<JsonElement>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessEventAsync_BookingCreated_NotifiesCustomerAndReceptionists()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new
        {
            bookingId = Guid.NewGuid(),
            bookingReference = "TH-2026-998877",
            customerId = customerId.ToString(),
            totalAmount = 25000
        });

        _notificationServiceMock
            .Setup(s => s.SendNotificationAsync(
                customerId,
                "Booking Confirmed",
                It.Is<string>(m => m.Contains("TH-2026-998877")),
                NotificationType.BookingCreated,
                payload,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationDto(
                Guid.NewGuid(), customerId, "Booking Confirmed", "Confirmed",
                NotificationType.BookingCreated, payload, false, DateTime.UtcNow, null));

        // Act
        await _consumer.ProcessEventAsync("booking.created", payload);

        // Assert
        _notificationServiceMock.Verify(s => s.SendNotificationAsync(
            customerId,
            "Booking Confirmed",
            It.Is<string>(m => m.Contains("TH-2026-998877")),
            NotificationType.BookingCreated,
            payload,
            It.IsAny<CancellationToken>()), Times.Once);

        _dispatcherMock.Verify(d => d.SendNotificationToGroupAsync(
            "role_Receptionist",
            It.Is<NotificationDto>(dto => dto.Title == "New Booking Created" && dto.Message.Contains("TH-2026-998877")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessEventAsync_PaymentRefunded_NotifiesCustomerAndManagers()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new
        {
            paymentId = Guid.NewGuid(),
            bookingReference = "TH-2026-112233",
            customerId = customerId.ToString(),
            refundAmount = 18000
        });

        _notificationServiceMock
            .Setup(s => s.SendNotificationAsync(
                customerId,
                "Refund Processed",
                It.Is<string>(m => m.Contains("TH-2026-112233")),
                NotificationType.PaymentRefunded,
                payload,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationDto(
                Guid.NewGuid(), customerId, "Refund Processed", "Refunded",
                NotificationType.PaymentRefunded, payload, false, DateTime.UtcNow, null));

        // Act
        await _consumer.ProcessEventAsync("payment.refunded", payload);

        // Assert
        _notificationServiceMock.Verify(s => s.SendNotificationAsync(
            customerId,
            "Refund Processed",
            It.Is<string>(m => m.Contains("TH-2026-112233")),
            NotificationType.PaymentRefunded,
            payload,
            It.IsAny<CancellationToken>()), Times.Once);

        _dispatcherMock.Verify(d => d.SendNotificationToGroupAsync(
            "role_Manager",
            It.Is<NotificationDto>(dto => dto.Title == "Payment Refunded" && dto.Message.Contains("TH-2026-112233")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
