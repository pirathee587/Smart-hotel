using System.ComponentModel.DataAnnotations;

namespace SmartHotel.Identity.Application.Features.Auth.Models;

public class RequestMagicLinkCommand
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public RequestMagicLinkCommand() { }

    public RequestMagicLinkCommand(string email)
    {
        Email = email;
    }
}

public class VerifyMagicLinkCommand
{
    [Required]
    public string Token { get; set; } = string.Empty;

    public VerifyMagicLinkCommand() { }

    public VerifyMagicLinkCommand(string token)
    {
        Token = token;
    }
}

public class RequestMagicLinkResponse
{
    public string Message { get; set; } = "If an account with this email exists, a magic login link has been sent.";
}
