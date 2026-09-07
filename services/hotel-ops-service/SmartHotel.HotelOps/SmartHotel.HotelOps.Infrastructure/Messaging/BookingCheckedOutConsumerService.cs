using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SmartHotel.HotelOps.Domain.Enums;
using SmartHotel.HotelOps.Infrastructure.Persistence;

namespace SmartHotel.HotelOps.Infrastructure.Messaging;

public class BookingCheckedOutConsumerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BookingCheckedOutConsumerService> _logger;

    private IConnection? _connection;
    private IModel? _channel;
    private const string ExchangeName = "smarthotel.events";
    private const string QueueName = "hotelops.booking-events";
    private const string RoutingKey = "booking.checkedout";

    public BookingCheckedOutConsumerService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<BookingCheckedOutConsumerService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ = Task.Run(() => StartListening(stoppingToken), stoppingToken);
        return Task.CompletedTask;
    }

    private void StartListening(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (ConnectAndSubscribe(ct))
                {
                    // Block until cancellation requested or connection closed
                    while (!ct.IsCancellationRequested && _channel != null && _channel.IsOpen)
                    {
                        Thread.Sleep(2000);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("RabbitMQ consumer error: {Message}. Retrying in 5 seconds...", ex.Message);
            }

            if (!ct.IsCancellationRequested)
            {
                Thread.Sleep(5000);
            }
        }
    }

    private bool ConnectAndSubscribe(CancellationToken ct)
    {
        try
        {
            var host = _configuration["RABBITMQ_HOST"] ?? _configuration["RabbitMQ:Host"] ?? "rabbitmq";
            var port = int.TryParse(_configuration["RABBITMQ_PORT"] ?? _configuration["RabbitMQ:Port"], out var p) ? p : 5672;
            var user = _configuration["RABBITMQ_DEFAULT_USER"] ?? _configuration["RabbitMQ:User"] ?? "guest";
            var pass = _configuration["RABBITMQ_DEFAULT_PASS"] ?? _configuration["RabbitMQ:Pass"] ?? "guest";

            var factory = new ConnectionFactory
            {
                HostName = host,
                Port = port,
                UserName = user,
                Password = pass,
                RequestedHeartbeat = TimeSpan.FromSeconds(15)
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.ExchangeDeclare(exchange: ExchangeName, type: ExchangeType.Topic, durable: true);
            _channel.QueueDeclare(queue: QueueName, durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind(queue: QueueName, exchange: ExchangeName, routingKey: RoutingKey);

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                _logger.LogInformation("Received {RoutingKey} message: {Message}", ea.RoutingKey, message);

                try
                {
                    await ProcessCheckoutMessageAsync(message, ct);
                    _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process booking.checkedout message: {Message}", message);
                    _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                }
            };

            _channel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);
            _logger.LogInformation("Subscribed to {QueueName} on exchange {ExchangeName} ({RoutingKey})", QueueName, ExchangeName, RoutingKey);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Could not connect RabbitMQ consumer: {Message}", ex.Message);
            return false;
        }
    }

    private async Task ProcessCheckoutMessageAsync(string json, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(json);
        Guid? roomId = null;

        if (doc.RootElement.TryGetProperty("RoomId", out var roomIdProp))
        {
            if (roomIdProp.TryGetGuid(out var id))
            {
                roomId = id;
            }
            else if (Guid.TryParse(roomIdProp.GetString(), out var idStr))
            {
                roomId = idStr;
            }
        }

        if (!roomId.HasValue)
        {
            _logger.LogWarning("Checkout message did not contain a valid RoomId: {Json}", json);
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HotelOpsDbContext>();

        var room = await context.Rooms.FirstOrDefaultAsync(r => r.Id == roomId.Value, ct);
        if (room == null)
        {
            _logger.LogWarning("Room with ID {RoomId} not found in HotelOps database.", roomId.Value);
            return;
        }

        _logger.LogInformation("Auto-transitioning room {RoomNumber} from {PreviousStatus} to Dirty upon guest checkout.",
            room.RoomNumber, room.Status);

        // Transition room to Dirty (Occupied -> Dirty or Available -> Dirty if test)
        room.UpdateStatus(RoomStatus.Dirty);
        await context.SaveChangesAsync(ct);
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
