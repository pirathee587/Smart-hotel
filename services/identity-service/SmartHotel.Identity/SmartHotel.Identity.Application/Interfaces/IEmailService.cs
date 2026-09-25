namespace SmartHotel.Identity.Application.Interfaces;

public interface IEmailService
{
    Task SendEmailVerificationAsync(string recipientEmail, string recipientName, string token, string preferredLanguage = "en", CancellationToken cancellationToken = default);
    Task SendPasswordResetAsync(string recipientEmail, string recipientName, string token, string preferredLanguage = "en", CancellationToken cancellationToken = default);
    Task SendMagicLinkAsync(string recipientEmail, string recipientName, string token, string preferredLanguage = "en", CancellationToken cancellationToken = default);
    Task SendWelcomeWithCredentialsAsync(string recipientEmail, string recipientName, string temporaryPassword, CancellationToken cancellationToken = default);
}
