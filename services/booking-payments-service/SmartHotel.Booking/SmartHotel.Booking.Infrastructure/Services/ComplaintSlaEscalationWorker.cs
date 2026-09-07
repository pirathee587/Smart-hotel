using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartHotel.Booking.Application.Features.Complaints.Commands;

namespace SmartHotel.Booking.Infrastructure.Services;

public class ComplaintSlaEscalationWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ComplaintSlaEscalationWorker> _logger;

    public ComplaintSlaEscalationWorker(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<ComplaintSlaEscalationWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ComplaintSlaEscalationWorker started.");

        var intervalMinutes = int.TryParse(_configuration["SLA_CHECK_INTERVAL_MINUTES"], out var mins) ? mins : 5;
        var interval = TimeSpan.FromMinutes(intervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                var result = await mediator.Send(new AutoEscalateOverdueComplaintsCommand(), stoppingToken);
                if (result.Succeeded && result.Data > 0)
                {
                    _logger.LogWarning("SLA Escalation Worker auto-escalated {Count} overdue complaint(s).", result.Data);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during complaint SLA evaluation cycle.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
