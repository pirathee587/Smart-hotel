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
using SmartHotel.HotelOps.Domain.Entities;
using SmartHotel.HotelOps.Infrastructure.Persistence;
using System.Security.Cryptography;

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
    private const string CheckoutRoutingKey = "booking.checkedout";
    private const string CheckInRoutingKey = "booking.checkedin";

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
            _channel.QueueBind(queue: QueueName, exchange: ExchangeName, routingKey: CheckoutRoutingKey);
            _channel.QueueBind(queue: QueueName, exchange: ExchangeName, routingKey: CheckInRoutingKey);

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                _logger.LogInformation("Received {RoutingKey} message: {Message}", ea.RoutingKey, message);

                try
                {
                    if(ea.RoutingKey==CheckInRoutingKey)await ProcessCheckInMessageAsync(message,ct);else await ProcessCheckoutMessageAsync(message, ct);
                    _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process booking.checkedout message: {Message}", message);
                    _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                }
            };

            _channel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);
            _logger.LogInformation("Subscribed to {QueueName} on exchange {ExchangeName} for Booking check-in/out events", QueueName, ExchangeName);
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
        Guid? checkoutEventId = null;

        foreach (var name in new[] { "BookingId", "bookingId" })
        {
            if (doc.RootElement.TryGetProperty(name, out var eventProp) && (eventProp.TryGetGuid(out var eventGuid) || Guid.TryParse(eventProp.GetString(), out eventGuid))) { checkoutEventId=eventGuid; break; }
        }

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

        if (!roomId.HasValue || !checkoutEventId.HasValue)
        {
            _logger.LogWarning("Checkout message did not contain valid BookingId and RoomId values: {Json}", json);
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HotelOpsDbContext>();

        if (await context.OperationalEventReceipts.AsNoTracking().AnyAsync(x => x.EventId == checkoutEventId.Value, ct))
        {
            _logger.LogInformation("Duplicate checkout event {EventId} ignored.", checkoutEventId.Value);
            return;
        }

        var room = await context.Rooms.FirstOrDefaultAsync(r => r.Id == roomId.Value, ct);
        if (room == null)
        {
            _logger.LogWarning("Room with ID {RoomId} not found in HotelOps database.", roomId.Value);
            return;
        }

        if (room.Status == RoomStatus.Dirty) { _logger.LogInformation("Duplicate checkout event ignored for already-dirty room {RoomNumber}.", room.RoomNumber); return; }
        _logger.LogInformation("Auto-transitioning room {RoomNumber} from {PreviousStatus} to Dirty upon guest checkout.",
            room.RoomNumber, room.Status);

        // Transition room to Dirty (Occupied -> Dirty or Available -> Dirty if test)
        room.UpdateStatus(RoomStatus.Dirty);
        context.OperationalEventReceipts.Add(new OperationalEventReceipt { EventId=checkoutEventId.Value,TaskId=checkoutEventId.Value,RoomId=room.Id,EventType="booking.checkedout",OccurredAtUtc=DateTime.UtcNow });
        await context.SaveChangesAsync(ct);
    }

    private async Task ProcessCheckInMessageAsync(string json,CancellationToken ct)
    {
        using var doc=JsonDocument.Parse(json);if(!TryGuid(doc,"BookingId",out var bookingId)||!TryGuid(doc,"RoomId",out var roomId)){_logger.LogWarning("Check-in event missing BookingId/RoomId");return;}
        var eventId=StableEvent(bookingId,"booking.checkedin");using var scope=_serviceProvider.CreateScope();var context=scope.ServiceProvider.GetRequiredService<HotelOpsDbContext>();
        if(await context.OperationalEventReceipts.AsNoTracking().AnyAsync(x=>x.EventId==eventId,ct))return;
        var room=await context.Rooms.FirstOrDefaultAsync(x=>x.Id==roomId,ct);if(room is null){_logger.LogWarning("Check-in room {RoomId} not found",roomId);return;}
        var blocked=await context.MaintenanceRestrictions.AnyAsync(x=>x.RoomId==roomId&&x.Status==MaintenanceRestrictionStatus.Active,ct);var latestCheckout=await context.OperationalEventReceipts.Where(x=>x.RoomId==roomId&&x.EventType=="booking.checkedout").MaxAsync(x=>(DateTime?)x.ProcessedAtUtc,ct);var approved=await context.HousekeepingReadinessRecords.AnyAsync(x=>x.RoomId==roomId&&x.Status==HousekeepingReadinessStatus.InspectionApproved&&x.InspectedAtUtc.HasValue&&(!latestCheckout.HasValue||x.InspectedAtUtc>=latestCheckout),ct);
        if(room.Status!=RoomStatus.Available||blocked||!approved)throw new InvalidOperationException("Authoritative room readiness changed before occupancy transition.");
        room.UpdateStatus(RoomStatus.Occupied);context.OperationalEventReceipts.Add(new(){EventId=eventId,TaskId=bookingId,RoomId=roomId,EventType="booking.checkedin",OccurredAtUtc=DateTime.UtcNow});await context.SaveChangesAsync(ct);
    }

    private static bool TryGuid(JsonDocument doc,string name,out Guid id){id=Guid.Empty;foreach(var n in new[]{name,char.ToLowerInvariant(name[0])+name[1..]})if(doc.RootElement.TryGetProperty(n,out var p)&&(p.TryGetGuid(out id)||Guid.TryParse(p.GetString(),out id)))return true;return false;}
    private static Guid StableEvent(Guid id,string stage)=>new(MD5.HashData(Encoding.UTF8.GetBytes(id+":"+stage)));

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
