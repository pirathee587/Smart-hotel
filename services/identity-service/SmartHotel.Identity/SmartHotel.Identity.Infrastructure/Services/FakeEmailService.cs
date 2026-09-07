using Microsoft.Extensions.Logging;
using SmartHotel.Identity.Application.Interfaces;

namespace SmartHotel.Identity.Infrastructure.Services;

public class FakeEmailService : IEmailService
{
    private readonly ILogger<FakeEmailService> _logger;

    public FakeEmailService(ILogger<FakeEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendEmailVerificationAsync(string recipientEmail, string recipientName, string token, string preferredLanguage = "en", CancellationToken cancellationToken = default)
    {
        var verificationUrl = $"https://localhost:5001/api/v1/auth/verify-email?token={token}&email={Uri.EscapeDataString(recipientEmail)}";

        _logger.LogInformation("================================================================================");
        _logger.LogInformation(" [FAKE EMAIL SERVICE] Email Verification Sent");
        _logger.LogInformation(" To: {RecipientName} <{RecipientEmail}>", recipientName, recipientEmail);
        _logger.LogInformation(" Language: {Language}", preferredLanguage);
        _logger.LogInformation(" Verification Token: {Token}", token);
        _logger.LogInformation(" Click to verify: {Url}", verificationUrl);
        _logger.LogInformation("================================================================================");

        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string recipientEmail, string recipientName, string token, string preferredLanguage = "en", CancellationToken cancellationToken = default)
    {
        var resetUrl = $"https://localhost:5001/api/v1/auth/reset-password?token={token}&email={Uri.EscapeDataString(recipientEmail)}";

        _logger.LogInformation("================================================================================");
        _logger.LogInformation(" [FAKE EMAIL SERVICE] Password Reset Sent");
        _logger.LogInformation(" To: {RecipientName} <{RecipientEmail}>", recipientName, recipientEmail);
        _logger.LogInformation(" Language: {Language}", preferredLanguage);
        _logger.LogInformation(" Reset Token: {Token}", token);
        _logger.LogInformation(" Click to reset: {Url}", resetUrl);
        _logger.LogInformation("================================================================================");

        return Task.CompletedTask;
    }

    public Task SendMagicLinkAsync(string recipientEmail, string recipientName, string token, string preferredLanguage = "en", CancellationToken cancellationToken = default)
    {
        var magicLinkUrl = $"https://localhost:5001/api/v1/portal-auth/verify-magic-link?token={token}";

        _logger.LogInformation("================================================================================");
        _logger.LogInformation(" [FAKE EMAIL SERVICE] Magic Link Login Sent");
        _logger.LogInformation(" To: {RecipientName} <{RecipientEmail}>", recipientName, recipientEmail);
        _logger.LogInformation(" Language: {Language}", preferredLanguage);
        _logger.LogInformation(" Magic Link Token: {Token}", token);
        _logger.LogInformation(" Click to log in: {Url}", magicLinkUrl);
        _logger.LogInformation("================================================================================");

        return Task.CompletedTask;
    }
}
