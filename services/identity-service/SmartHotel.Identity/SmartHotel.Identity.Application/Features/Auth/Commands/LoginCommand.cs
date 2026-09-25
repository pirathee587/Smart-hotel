using System.ComponentModel.DataAnnotations;

namespace SmartHotel.Identity.Application.Features.Auth.Commands;

public class LoginCommand
{
    [Required(ErrorMessage = "Username is required")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = string.Empty;
}

public class MemberDto
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}

public class MemberLoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; } = 3600;
    public MemberDto Member { get; set; } = new();
}

public class LoginCommandResult
{
    public bool Succeeded { get; set; }
    public string Error { get; set; } = string.Empty;
    public MemberLoginResponse? Data { get; set; }

    public static LoginCommandResult Success(MemberLoginResponse data) =>
        new() { Succeeded = true, Data = data };

    public static LoginCommandResult InvalidCredentials() =>
        new() { Succeeded = false, Error = "invalid_credentials" };
}
