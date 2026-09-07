using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SmartHotel.Notifications.Application;
using SmartHotel.Notifications.Domain;

namespace SmartHotel.Notifications.Infrastructure.Messaging;

public class NotificationEventConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationEventConsumer> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public NotificationEventConsumer(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<NotificationEventConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var host = _configuration["RabbitMQ:Host"] ?? _configuration["RABBITMQ_HOST"] ?? "rabbitmq";
        var portStr = _configuration["RabbitMQ:Port"] ?? _configuration["RABBITMQ_PORT"] ?? "5672";
        _ = int.TryParse(portStr, out var port);

        // Run consumer loop in background
        _ = Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var factory = new ConnectionFactory
                    {
                        HostName = host,
                        Port = port > 0 ? port : 5672,
                        UserName = _configuration["RabbitMQ:UserName"] ?? "guest",
                        Password = _configuration["RabbitMQ:Password"] ?? "guest",
                        DispatchConsumersAsync = true
                    };

                    _connection = factory.CreateConnection();
                    _channel = _connection.CreateModel();

                    const string exchange = "smarthotel.events";
                    const string queue = "smarthotel.notifications.events";

                    _channel.ExchangeDeclare(exchange, ExchangeType.Topic, durable: true);
                    _channel.QueueDeclare(queue, durable: true, exclusive: false, autoDelete: false);

                    // Bind routing keys
                    string[] routingKeys =
                    {
                        "task.dispatched",
                        "room.status_changed",
                        "kds.status_changed",
                        "booking.created",
                        "booking.checkedout",
                        "payment.refunded"
                    };

                    foreach (var routingKey in routingKeys)
                    {
                        _channel.QueueBind(queue, exchange, routingKey);
                    }

                    var consumer = new AsyncEventingBasicConsumer(_channel);
                    consumer.Received += async (sender, ea) =>
                    {
                        var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);
                        var routingKey = ea.RoutingKey;

                        try
                        {
                            await ProcessEventAsync(routingKey, message, stoppingToken);
                            _channel.BasicAck(ea.DeliveryTag, false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing event with routing key {RoutingKey}: {Message}", routingKey, message);
                            _channel.BasicNack(ea.DeliveryTag, false, requeue: false);
                        }
                    };

                    _channel.BasicConsume(queue, autoAck: false, consumer);
                    _logger.LogInformation("NotificationEventConsumer connected and listening on {Queue} with exchange {Exchange}", queue, exchange);

                    // Wait until stopped
                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "RabbitMQ connection failed in NotificationEventConsumer. Retrying in 10s...");
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }
        }, stoppingToken);

        return Task.CompletedTask;
    }

    public async Task ProcessEventAsync(string routingKey, string message, CancellationToken ct = default)
    {
        _logger.LogInformation("Received RabbitMQ event {RoutingKey}: {Payload}", routingKey, message);

        using var scope = _scopeFactory.CreateScope();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ISignalRNotificationDispatcher>();

        using var doc = JsonDocument.Parse(message);
        var root = doc.RootElement;

        switch (routingKey)
        {
            case "task.dispatched":
                await HandleTaskDispatchedAsync(root, message, notificationService, dispatcher, ct);
                break;

            case "room.status_changed":
                await HandleRoomStatusChangedAsync(root, dispatcher, ct);
                break;

            case "kds.status_changed":
                await HandleKdsStatusChangedAsync(root, dispatcher, ct);
                break;

            case "booking.created":
                await HandleBookingCreatedAsync(root, message, notificationService, dispatcher, ct);
                break;

            case "payment.refunded":
                await HandlePaymentRefundedAsync(root, message, notificationService, dispatcher, ct);
                break;

            default:
                _logger.LogDebug("No specific handler registered for routing key {RoutingKey}", routingKey);
                break;
        }
    }

    private async Task HandleTaskDispatchedAsync(
        JsonElement root,
        string rawJson,
        INotificationService notificationService,
        ISignalRNotificationDispatcher dispatcher,
        CancellationToken ct)
    {
        string? empIdStr = GetProperty(root, "assignedEmployeeId") ?? GetProperty(root, "AssignedEmployeeId");
        if (!Guid.TryParse(empIdStr, out var employeeId))
            return;

        string title = GetProperty(root, "title") ?? GetProperty(root, "Title") ?? "Task Assigned";
        string priority = GetProperty(root, "priority") ?? GetProperty(root, "Priority") ?? "Normal";

        // 1. Write to DB (offline fallback)
        var notif = await notificationService.SendNotificationAsync(
            employeeId,
            "New Task Assigned",
            $"You have been assigned to task: '{title}' (Priority: {priority})",
            NotificationType.TaskAssigned,
            rawJson,
            ct);

        // 2. Broadcast specific TaskAssigned event to user group
        await dispatcher.BroadcastTaskAssignedAsync(employeeId, root, ct);
    }

    private async Task HandleRoomStatusChangedAsync(
        JsonElement root,
        ISignalRNotificationDispatcher dispatcher,
        CancellationToken ct)
    {
        // Broadcast to relevant staff roles
        string[] targetRoles = { "role_Housekeeper", "role_Manager", "role_Admin", "role_Receptionist" };
        await dispatcher.BroadcastRoomStatusUpdatedAsync(targetRoles, root, ct);
    }

    private async Task HandleKdsStatusChangedAsync(
        JsonElement root,
        ISignalRNotificationDispatcher dispatcher,
        CancellationToken ct)
    {
        string status = GetProperty(root, "status") ?? GetProperty(root, "Status") ?? string.Empty;

        // Broadcast delivery alert when food is ready
        if (status.Equals("Ready", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("Delivered", StringComparison.OrdinalIgnoreCase))
        {
            string[] targetRoles = { "role_Waiter", "role_Manager" };
            await dispatcher.BroadcastDeliveryAlertAsync(targetRoles, root, ct);
        }
    }

    private async Task HandleBookingCreatedAsync(
        JsonElement root,
        string rawJson,
        INotificationService notificationService,
        ISignalRNotificationDispatcher dispatcher,
        CancellationToken ct)
    {
        string? custIdStr = GetProperty(root, "customerId") ?? GetProperty(root, "CustomerId");
        string bookingRef = GetProperty(root, "bookingReference") ?? GetProperty(root, "BookingReference") ?? "Reservation";

        if (Guid.TryParse(custIdStr, out var customerId))
        {
            await notificationService.SendNotificationAsync(
                customerId,
                "Booking Confirmed",
                $"Your reservation {bookingRef} has been confirmed successfully.",
                NotificationType.BookingCreated,
                rawJson,
                ct);
        }

        // Notify receptionists
        await dispatcher.SendNotificationToGroupAsync("role_Receptionist", new NotificationDto(
            Guid.NewGuid(),
            Guid.Empty,
            "New Booking Created",
            $"New booking received: {bookingRef}",
            NotificationType.BookingCreated,
            rawJson,
            false,
            DateTime.UtcNow,
            null
        ), ct);
    }

    private async Task HandlePaymentRefundedAsync(
        JsonElement root,
        string rawJson,
        INotificationService notificationService,
        ISignalRNotificationDispatcher dispatcher,
        CancellationToken ct)
    {
        string? custIdStr = GetProperty(root, "customerId") ?? GetProperty(root, "CustomerId");
        string bookingRef = GetProperty(root, "bookingReference") ?? GetProperty(root, "BookingReference") ?? "Reservation";

        if (Guid.TryParse(custIdStr, out var customerId))
        {
            await notificationService.SendNotificationAsync(
                customerId,
                "Refund Processed",
                $"A refund has been processed for reservation {bookingRef}.",
                NotificationType.PaymentRefunded,
                rawJson,
                ct);
        }

        // Notify managers
        await dispatcher.SendNotificationToGroupAsync("role_Manager", new NotificationDto(
            Guid.NewGuid(),
            Guid.Empty,
            "Payment Refunded",
            $"Refund issued for reservation {bookingRef}",
            NotificationType.PaymentRefunded,
            rawJson,
            false,
            DateTime.UtcNow,
            null
        ), ct);
    }

    private static string? GetProperty(JsonElement element, string propName)
    {
        if (element.TryGetProperty(propName, out var prop))
        {
            return prop.GetString();
        }
        return null;
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
