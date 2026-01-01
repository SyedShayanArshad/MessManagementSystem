using System.Net;
using System.Net.Mail;

namespace MessManagement.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string resetLink, string userName)
        {
            var smtpSettings = _configuration.GetSection("Smtp");
            var host = smtpSettings["Host"] ?? "smtp.gmail.com";
            var port = int.Parse(smtpSettings["Port"] ?? "587");
            var username = smtpSettings["Username"] ?? string.Empty;
            var password = smtpSettings["Password"] ?? string.Empty;
            var fromEmail = smtpSettings["FromEmail"] ?? username;
            var fromName = smtpSettings["FromName"] ?? "MessHub";
            var enableSsl = bool.Parse(smtpSettings["EnableSsl"] ?? "true");

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                _logger.LogWarning("SMTP credentials not configured. Password reset email not sent.");
                throw new InvalidOperationException("Email service is not properly configured. Please contact administrator.");
            }

            try
            {
                using var client = new SmtpClient(host, port)
                {
                    Credentials = new NetworkCredential(username, password),
                    EnableSsl = enableSsl
                };

                var message = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = "Password Reset Request - MessHub",
                    IsBodyHtml = true,
                    Body = GenerateEmailBody(userName, resetLink)
                };
                message.To.Add(toEmail);

                await client.SendMailAsync(message);
                _logger.LogInformation("Password reset email sent to {Email}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password reset email to {Email}", toEmail);
                throw new InvalidOperationException("Failed to send email. Please try again later.");
            }
        }

        private static string GenerateEmailBody(string userName, string resetLink)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, ""Helvetica Neue"", Arial, sans-serif; line-height: 1.6; margin: 0; padding: 0; background-color: #f4f4f5;'>
    <div style='max-width: 600px; margin: 0 auto; padding: 40px 20px;'>
        <div style='background: linear-gradient(135deg, #3b82f6 0%, #6366f1 100%); padding: 30px; border-radius: 16px 16px 0 0; text-align: center;'>
            <h1 style='color: white; margin: 0; font-size: 28px;'>🍽️ MessHub</h1>
            <p style='color: rgba(255,255,255,0.9); margin: 10px 0 0 0;'>Password Reset Request</p>
        </div>
        <div style='background: white; padding: 40px 30px; border-radius: 0 0 16px 16px; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
            <h2 style='color: #1f2937; margin: 0 0 20px 0;'>Hello {userName},</h2>
            <p style='color: #4b5563; margin: 0 0 20px 0;'>
                We received a request to reset your password. Click the button below to create a new password:
            </p>
            <div style='text-align: center; margin: 30px 0;'>
                <a href='{resetLink}' style='display: inline-block; background: linear-gradient(135deg, #3b82f6 0%, #6366f1 100%); color: white; padding: 14px 32px; text-decoration: none; border-radius: 8px; font-weight: 600; font-size: 16px;'>
                    Reset Password
                </a>
            </div>
            <p style='color: #6b7280; font-size: 14px; margin: 20px 0 0 0;'>
                This link will expire in <strong>1 hour</strong> for security reasons.
            </p>
            <p style='color: #6b7280; font-size: 14px; margin: 15px 0 0 0;'>
                If you didn't request a password reset, you can safely ignore this email. Your password will remain unchanged.
            </p>
            <hr style='border: none; border-top: 1px solid #e5e7eb; margin: 30px 0;'>
            <p style='color: #9ca3af; font-size: 12px; margin: 0; text-align: center;'>
                If the button doesn't work, copy and paste this link into your browser:<br>
                <a href='{resetLink}' style='color: #3b82f6; word-break: break-all;'>{resetLink}</a>
            </p>
        </div>
        <p style='color: #9ca3af; font-size: 12px; text-align: center; margin: 20px 0 0 0;'>
            © 2026 MessHub. All rights reserved.
        </p>
    </div>
</body>
</html>";
        }
    }
}
