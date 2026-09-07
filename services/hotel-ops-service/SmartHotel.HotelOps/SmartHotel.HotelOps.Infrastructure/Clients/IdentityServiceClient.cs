using Microsoft.Extensions.Logging;
using SmartHotel.HotelOps.Application.Interfaces;

namespace SmartHotel.HotelOps.Infrastructure.Clients;

public class IdentityServiceClient : IIdentityServiceClient
{
    private readonly ILogger<IdentityServiceClient> _logger;

    public IdentityServiceClient(ILogger<IdentityServiceClient> logger)
    {
        _logger = logger;
    }

    public Task<bool> ValidateManagerExistsAsync(Guid managerId, CancellationToken ct = default)
    {
        // Manager identity exists in Identity Service. Per architecture design,
        // this is stored as an unchecked Guid for now until Identity Service's gRPC
        // server contract is implemented.
        _logger.LogDebug("ManagerId {ManagerId} accepted via scaffolded IdentityServiceClient", managerId);
        return Task.FromResult(true);
    }
}
