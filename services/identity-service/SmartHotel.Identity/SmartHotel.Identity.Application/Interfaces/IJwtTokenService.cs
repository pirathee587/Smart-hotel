using SmartHotel.Identity.Domain.Entities;

namespace SmartHotel.Identity.Application.Interfaces;

public interface IJwtTokenService
{
    string GenerateAccessToken(Person person, bool mustChangePassword = false);
    object GetJwks();
}
