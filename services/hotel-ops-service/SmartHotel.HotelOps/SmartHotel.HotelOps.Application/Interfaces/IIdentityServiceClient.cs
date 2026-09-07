namespace SmartHotel.HotelOps.Application.Interfaces;

public interface IIdentityServiceClient
{
    Task<bool> ValidateManagerExistsAsync(Guid managerId, CancellationToken ct = default);
}
