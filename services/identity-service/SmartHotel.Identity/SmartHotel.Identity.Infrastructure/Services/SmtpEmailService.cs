using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using SmartHotel.Identity.Application.Interfaces;

namespace SmartHotel.Identity.Infrastructure.Services;

public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendEmailVerificationAsync(string recipientEmail, string recipientName, string token, string preferredLanguage = "en", CancellationToken cancellationToken = default)
    {
        var frontendUrl = _configuration["Frontend:Url"] ?? "http://localhost:3000";
        var verificationUrl = $"{frontendUrl}/portal/verify-email?token={token}&email={Uri.EscapeDataString(recipientEmail)}";

        var subject = "Verify your SmartHotel account";
        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #0F1B1A; color: #E7EFEC; padding: 20px; }}
        .card {{ background-color: #16302C; border: 1px solid rgba(231,239,236,0.15); border-radius: 12px; padding: 32px; max-width: 520px; margin: 0 auto; }}
        .header {{ font-size: 24px; font-weight: bold; color: #E7EFEC; margin-bottom: 16px; }}
        .accent {{ color: #E07A3E; font-style: italic; }}
        .btn {{ display: inline-block; background-color: #C4622D; color: #FFFFFF !important; text-decoration: none; padding: 12px 28px; border-radius: 8px; font-weight: 600; margin: 24px 0; }}
        .footer {{ font-size: 12px; color: #9BAFA9; margin-top: 24px; border-top: 1px solid rgba(231,239,236,0.1); padding-top: 16px; }}
    </style>
</head>
<body>
    <div class='card'>
        <div class='header'>Smart<span class='accent'>Hotel</span></div>
        <h2>Welcome, {recipientName}!</h2>
        <p>Thank you for creating an account with SmartHotel Maskeliya. Please verify your email address to activate your account.</p>
        <a href='{verificationUrl}' class='btn'>Verify Email Address</a>
        <p>Or copy this link into your browser:</p>
        <p style='word-break: break-all; color: #9BAFA9; font-size: 13px;'>{verificationUrl}</p>
        <div class='footer'>
            If you did not register for a SmartHotel account, please disregard this message.
        </div>
    </div>
</body>
</html>";

        await SendEmailInternalAsync(recipientEmail, recipientName, subject, htmlBody, verificationUrl, "Verification Link", cancellationToken);
    }

    public async Task SendPasswordResetAsync(string recipientEmail, string recipientName, string token, string preferredLanguage = "en", CancellationToken cancellationToken = default)
    {
        var frontendUrl = _configuration["Frontend:Url"] ?? "http://localhost:3000";
        var resetUrl = $"{frontendUrl}/portal/reset-password?token={token}&email={Uri.EscapeDataString(recipientEmail)}";

        var subject = "Reset your SmartHotel password";
        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #0F1B1A; color: #E7EFEC; padding: 20px; }}
        .card {{ background-color: #16302C; border: 1px solid rgba(231,239,236,0.15); border-radius: 12px; padding: 32px; max-width: 520px; margin: 0 auto; }}
        .header {{ font-size: 24px; font-weight: bold; color: #E7EFEC; margin-bottom: 16px; }}
        .accent {{ color: #E07A3E; font-style: italic; }}
        .btn {{ display: inline-block; background-color: #C4622D; color: #FFFFFF !important; text-decoration: none; padding: 12px 28px; border-radius: 8px; font-weight: 600; margin: 24px 0; }}
        .footer {{ font-size: 12px; color: #9BAFA9; margin-top: 24px; border-top: 1px solid rgba(231,239,236,0.1); padding-top: 16px; }}
    </style>
</head>
<body>
    <div class='card'>
        <div class='header'>Smart<span class='accent'>Hotel</span></div>
        <h2>Password Reset Request</h2>
        <p>Hello {recipientName},</p>
        <p>We received a request to reset the password for your SmartHotel account. Click the button below to set a new password:</p>
        <a href='{resetUrl}' class='btn'>Reset Password</a>
        <p>Or copy this link into your browser:</p>
        <p style='word-break: break-all; color: #9BAFA9; font-size: 13px;'>{resetUrl}</p>
        <div class='footer'>
            This link is valid for 1 hour. If you didn't request a password reset, you can safely ignore this email.
        </div>
    </div>
</body>
</html>";

        await SendEmailInternalAsync(recipientEmail, recipientName, subject, htmlBody, resetUrl, "Reset Link", cancellationToken);
    }

    public async Task SendMagicLinkAsync(string recipientEmail, string recipientName, string token, string preferredLanguage = "en", CancellationToken cancellationToken = default)
    {
        var frontendUrl = _configuration["Frontend:Url"] ?? "http://localhost:3000";
        var magicLinkUrl = $"{frontendUrl}/portal/verify-magic-link?token={token}";

        var subject = "Your SmartHotel Magic Sign-In Link";
        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #0F1B1A; color: #E7EFEC; padding: 20px; }}
        .card {{ background-color: #16302C; border: 1px solid rgba(231,239,236,0.15); border-radius: 12px; padding: 32px; max-width: 520px; margin: 0 auto; }}
        .header {{ font-size: 24px; font-weight: bold; color: #E7EFEC; margin-bottom: 16px; }}
        .accent {{ color: #E07A3E; font-style: italic; }}
        .btn {{ display: inline-block; background-color: #C4622D; color: #FFFFFF !important; text-decoration: none; padding: 12px 28px; border-radius: 8px; font-weight: 600; margin: 24px 0; }}
        .footer {{ font-size: 12px; color: #9BAFA9; margin-top: 24px; border-top: 1px solid rgba(231,239,236,0.1); padding-top: 16px; }}
    </style>
</head>
<body>
    <div class='card'>
        <div class='header'>Smart<span class='accent'>Hotel</span></div>
        <h2>Instant Access Sign-In</h2>
        <p>Hello {recipientName},</p>
        <p>Click the link below to sign in instantly without a password:</p>
        <a href='{magicLinkUrl}' class='btn'>Sign In to SmartHotel</a>
        <p>Or copy this link into your browser:</p>
        <p style='word-break: break-all; color: #9BAFA9; font-size: 13px;'>{magicLinkUrl}</p>
        <div class='footer'>
            This single-use link expires in 15 minutes.
        </div>
    </div>
</body>
</html>";

        await SendEmailInternalAsync(recipientEmail, recipientName, subject, htmlBody, magicLinkUrl, "Magic Link", cancellationToken);
    }

    private async Task SendEmailInternalAsync(
        string recipientEmail,
        string recipientName,
        string subject,
        string htmlBody,
        string directLink,
        string linkType,
        CancellationToken cancellationToken)
    {
        var host = _configuration["Smtp:Host"] ?? "smtp.gmail.com";
        var portStr = _configuration["Smtp:Port"];
        var port = int.TryParse(portStr, out var p) ? p : 587;
        var senderEmail = _configuration["Smtp:SenderEmail"]
            ?? _configuration["Smtp:Username"]
            ?? "jeyakumaranpiratheepan20@gmail.com";
        var senderName = _configuration["Smtp:SenderName"] ?? "SmartHotel";
        var username = _configuration["Smtp:Username"] ?? senderEmail;
        var password = _configuration["Smtp:Password"]
            ?? Environment.GetEnvironmentVariable("SMTP_PASSWORD")
            ?? "ovvlcynridcagszh";

        // Remove whitespace from App Password if present
        password = password.Replace(" ", "").Trim();

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(senderName, senderEmail));
            message.To.Add(new MailboxAddress(string.IsNullOrWhiteSpace(recipientName) ? recipientEmail : recipientName, recipientEmail));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlBody };

            using var client = new SmtpClient();
            var security = port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            await client.ConnectAsync(host, port, security, cancellationToken);
            
            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
            {
                await client.AuthenticateAsync(username, password, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("[SMTP] Email '{Subject}' successfully sent to {Recipient} via {Host}:{Port}",
                subject, recipientEmail, host, port);
        }
        catch (Exception ex)
        {
            // Log full warning details and preserve link in console so registration/recovery is never blocked
            _logger.LogWarning(ex, "[SMTP] Failed to send email '{Subject}' to {Recipient} via {Host}:{Port}. Fallback direct link: {DirectLink}",
                subject, recipientEmail, host, port, directLink);

            _logger.LogInformation("================================================================================");
            _logger.LogInformation(" [EMAIL FALLBACK NOTIFICATION]");
            _logger.LogInformation(" To: {RecipientName} <{RecipientEmail}>", recipientName, recipientEmail);
            _logger.LogInformation(" Subject: {Subject}", subject);
            _logger.LogInformation(" {LinkType}: {DirectLink}", linkType, directLink);
            _logger.LogInformation("================================================================================");
        }
    }
}
