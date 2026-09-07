using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using SmartHotel.Booking.Infrastructure.Persistence;

namespace SmartHotel.Booking.Infrastructure.Services;

public class OutboxPublisherService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OutboxPublisherService> _logger;

    private IConnection? _connection;
    private IModel? _channel;
    private readonly string _exchangeName = "smarthotel.events";

    public OutboxPublisherService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<OutboxPublisherService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Booking OutboxPublisherService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing outbox messages. Will retry in next polling cycle.");
            }

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        var messages = await context.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(20)
            .ToListAsync(ct);

        if (messages.Count == 0)
        {
            return;
        }

        if (!EnsureRabbitMqConnected())
        {
            _logger.LogDebug("RabbitMQ unavailable. Outbox messages remain safely queued in database.");
            return;
        }

        foreach (var message in messages)
        {
            try
            {
                var body = Encoding.UTF8.GetBytes(message.Content);
                var props = _channel!.CreateBasicProperties();
                props.Persistent = true;
                props.MessageId = message.Id.ToString();
                props.ContentType = "application/json";
                props.Type = message.Type;

                _channel.BasicPublish(
                    exchange: _exchangeName,
                    routingKey: message.Type,
                    basicProperties: props,
                    body: body);

                message.ProcessedOnUtc = DateTime.UtcNow;
                _logger.LogInformation("Booking Outbox message {Id} of type {Type} published to RabbitMQ.", message.Id, message.Type);
            }
            catch (Exception ex)
            {
                message.Error = ex.Message;
                _logger.LogWarning(ex, "Failed to publish outbox message {Id}.", message.Id);
            }
        }

        await context.SaveChangesAsync(ct);
    }

    private bool EnsureRabbitMqConnected()
    {
        if (_channel != null && _channel.IsOpen)
        {
            return true;
        }

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
            _channel.ExchangeDeclare(exchange: _exchangeName, type: ExchangeType.Topic, durable: true);

            _logger.LogInformation("Connected to RabbitMQ at {Host}:{Port} with exchange {Exchange}", host, port, _exchangeName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("RabbitMQ connection could not be established: {Message}", ex.Message);
            return false;
        }
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
