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
        var htmlBody = BuildEmailHtml(
            subject,
            $"Welcome, {recipientName}!",
            "<p style=\"margin: 0;\">Thank you for creating an account with SmartHotel Maskeliya. Please verify your email address to activate your account.</p>",
            "Verify Email Address",
            verificationUrl,
            "If you did not register for a SmartHotel account, please disregard this message."
        );

        await SendEmailInternalAsync(recipientEmail, recipientName, subject, htmlBody, verificationUrl, "Verification Link", cancellationToken);
    }

    public async Task SendPasswordResetAsync(string recipientEmail, string recipientName, string token, string preferredLanguage = "en", CancellationToken cancellationToken = default)
    {
        var frontendUrl = _configuration["Frontend:Url"] ?? "http://localhost:3000";
        var resetUrl = $"{frontendUrl}/portal/reset-password?token={token}&email={Uri.EscapeDataString(recipientEmail)}";

        var subject = "Reset your SmartHotel password";
        var htmlBody = BuildEmailHtml(
            subject,
            "Password Reset Request",
            $"<p style=\"margin: 0 0 10px 0;\">Hello {recipientName},</p><p style=\"margin: 0;\">We received a request to reset the password for your SmartHotel account. Click the button below to set a new password:</p>",
            "Reset Password",
            resetUrl,
            "This link is valid for 1 hour. If you didn't request a password reset, you can safely ignore this email."
        );

        await SendEmailInternalAsync(recipientEmail, recipientName, subject, htmlBody, resetUrl, "Reset Link", cancellationToken);
    }

    public async Task SendMagicLinkAsync(string recipientEmail, string recipientName, string token, string preferredLanguage = "en", CancellationToken cancellationToken = default)
    {
        var frontendUrl = _configuration["Frontend:Url"] ?? "http://localhost:3000";
        var magicLinkUrl = $"{frontendUrl}/portal/verify-magic-link?token={token}";

        var subject = "Your SmartHotel Magic Sign-In Link";
        var htmlBody = BuildEmailHtml(
            subject,
            "Instant Access Sign-In",
            $"<p style=\"margin: 0 0 10px 0;\">Hello {recipientName},</p><p style=\"margin: 0;\">Click the link below to sign in instantly without a password:</p>",
            "Sign In to SmartHotel",
            magicLinkUrl,
            "This single-use link expires in 15 minutes."
        );

        await SendEmailInternalAsync(recipientEmail, recipientName, subject, htmlBody, magicLinkUrl, "Magic Link", cancellationToken);
    }

    public async Task SendWelcomeWithCredentialsAsync(string recipientEmail, string recipientName, string temporaryPassword, CancellationToken cancellationToken = default)
    {
        var frontendUrl = _configuration["Frontend:Url"] ?? "http://localhost:3000";
        var loginUrl = $"{frontendUrl}/staff/login";

        var subject = "Welcome to SmartHotel — Your Login Credentials";
        var messageHtml = $@"
            <p style=""margin: 0 0 12px 0;"">Hello {recipientName},</p>
            <p style=""margin: 0 0 16px 0;"">An account has been created for you on the SmartHotel Management Portal. Below are your temporary login credentials:</p>
            <div style=""background-color: #EDEAE1; border: 1px solid #DBD7CC; border-radius: 8px; padding: 14px 18px; margin: 16px 0;"">
                <p style=""margin: 0 0 8px 0; font-size: 13px; color: #5C6A66;""><strong>Work Email:</strong> <span style=""font-family: Consolas, Monaco, monospace; color: #16302C;"">{recipientEmail}</span></p>
                <p style=""margin: 0; font-size: 13px; color: #5C6A66;""><strong>Temporary Password:</strong> <span style=""font-family: Consolas, Monaco, monospace; font-size: 16px; font-weight: 700; color: #C4622D; letter-spacing: 1px;"">{temporaryPassword}</span></p>
            </div>
            <p style=""margin: 16px 0 0 0; font-size: 13px; color: #5C6A66;"">
                <strong>Mandatory Password Reset:</strong> For security reasons, you will be required to change this temporary password immediately upon your first login.
            </p>";

        var htmlBody = BuildEmailHtml(
            subject,
            "Staff Account Created",
            messageHtml,
            "Log In to Staff Portal",
            loginUrl,
            "Please keep these credentials secure. Do not share your temporary password with anyone."
        );

        await SendEmailInternalAsync(recipientEmail, recipientName, subject, htmlBody, loginUrl, "Staff Login", cancellationToken);
    }

    private string BuildEmailHtml(
        string title,
        string heading,
        string messageHtml,
        string buttonText,
        string buttonUrl,
        string footerText)
    {
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{title}</title>
</head>
<body style=""margin: 0; padding: 0; background-color: #F7F6F2; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; -webkit-font-smoothing: antialiased;"">
    <table role=""presentation"" width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #F7F6F2; width: 100%; padding: 40px 16px; margin: 0;"">
        <tr>
            <td align=""center"" style=""padding: 0;"">
                <table role=""presentation"" width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""max-width: 540px; background-color: #F7F6F2; border: 1px solid #E2DED5; border-radius: 12px; margin: 0 auto; text-align: left;"">
                    <tr>
                        <td style=""padding: 36px 32px; background-color: #F7F6F2; border-radius: 12px;"">
                            <div style=""margin-bottom: 24px; padding-bottom: 18px; border-bottom: 1px solid #E2DED5;"">
                                <span style=""font-size: 26px; font-weight: 800; color: #16302C; letter-spacing: -0.5px;"">Smart</span><span style=""font-size: 26px; font-weight: 800; color: #C4622D; font-style: italic;"">Hotel</span>
                                <div style=""font-size: 11px; font-weight: 600; text-transform: uppercase; letter-spacing: 1.5px; color: #7A8884; margin-top: 3px;"">Maskeliya &bull; Luxury Stay</div>
                            </div>
                            <h2 style=""margin: 0 0 16px 0; color: #16302C; font-size: 22px; font-weight: 700; line-height: 1.3;"">{heading}</h2>
                            <div style=""margin: 0 0 24px 0; color: #2D3A37; font-size: 15px; line-height: 1.6;"">
                                {messageHtml}
                            </div>
                            <table role=""presentation"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""margin: 24px 0;"">
                                <tr>
                                    <td align=""center"" style=""border-radius: 8px; background-color: #C4622D;"">
                                        <a href=""{buttonUrl}"" target=""_blank"" style=""display: inline-block; padding: 14px 32px; font-size: 15px; font-weight: 600; color: #FFFFFF !important; text-decoration: none; border-radius: 8px; background-color: #C4622D; letter-spacing: 0.3px;"">
                                            {buttonText}
                                        </a>
                                    </td>
                                </tr>
                            </table>
                            <p style=""margin: 20px 0 8px 0; font-size: 13px; font-weight: 500; color: #5C6A66;"">
                                Or copy this link into your browser:
                            </p>
                            <div style=""background-color: #EDEAE1; border: 1px solid #DBD7CC; border-radius: 8px; padding: 12px 14px; word-break: break-all; margin-bottom: 24px;"">
                                <a href=""{buttonUrl}"" target=""_blank"" style=""color: #C4622D !important; font-size: 12px; line-height: 1.5; font-family: Consolas, Monaco, monospace; text-decoration: underline;"">
                                    {buttonUrl}
                                </a>
                            </div>
                            <div style=""font-size: 12px; color: #7A8884; border-top: 1px solid #E2DED5; padding-top: 18px; margin-top: 24px; line-height: 1.5;"">
                                {footerText}
                            </div>
                        </td>
                    </tr>
                </table>
                <div style=""margin-top: 18px; font-size: 11px; color: #9AA7A3; text-align: center;"">
                    &copy; SmartHotel Maskeliya. All rights reserved.
                </div>
            </td>
        </tr>
    </table>
</body>
</html>";
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
            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = htmlBody,
                TextBody = $"{subject}\n\n{linkType}: {directLink}\n\nIf this email contains temporary credentials, open the HTML version to view them securely."
            };
            message.Body = bodyBuilder.ToMessageBody();

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
