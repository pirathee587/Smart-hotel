using SmartHotel.Identity.Application.Interfaces;

namespace SmartHotel.Identity.Infrastructure.Services;

public class PasswordHasher : IPasswordHasher
{
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.EnhancedHashPassword(password, workFactor: 12);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(passwordHash))
            return false;

        return BCrypt.Net.BCrypt.EnhancedVerify(password, passwordHash);
    }
}
