namespace SmartHotel.Identity.Application.Features.Auth.Models;

public class AuthUserInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string? DepartmentCode { get; set; }
    public string? Designation { get; set; }
}

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
    public bool MustChangePassword { get; set; }
    public bool ProfileCompletionRequired { get; set; }
    public DateTime? ProfileCompletionDeadlineUtc { get; set; }
    public AuthUserInfo User { get; set; } = new();
    public Guid CustomerId => User?.Id ?? Guid.Empty;
    public string FullName => User != null ? (!string.IsNullOrEmpty(User.Name) ? User.Name : $"{User.FirstName} {User.LastName}".Trim()) : string.Empty;
    public string ExpiresAt => DateTime.UtcNow.AddSeconds(ExpiresIn > 0 ? ExpiresIn : 3600).ToString("o");
}

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class GoogleLoginRequest
{
    public string IdToken { get; set; } = string.Empty;
}

public class ForgotPasswordResponse
{
    public string Message { get; set; } = "If your email is registered in our system, you will receive password reset instructions shortly.";
}

public class CustomerRegistrationResponse
{
    public Guid CustomerId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Message { get; set; } = "Registration successful. Please check your email to verify your account.";
}
