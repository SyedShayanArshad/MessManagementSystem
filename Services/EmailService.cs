using System.Net;
using System.Net.Mail;
using MessManagement.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

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

        private async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
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
                _logger.LogWarning("SMTP credentials not configured. Email not sent.");
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
                    Subject = subject,
                    IsBodyHtml = true,
                    Body = htmlBody
                };
                message.To.Add(toEmail);

                await client.SendMailAsync(message);
                _logger.LogInformation("Email sent to {Email} with subject: {Subject}", toEmail, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
                throw new InvalidOperationException("Failed to send email. Please try again later.");
            }
        }

        private async Task SendEmailWithAttachmentAsync(string toEmail, string subject, string htmlBody, byte[] attachmentData, string attachmentName)
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
                _logger.LogWarning("SMTP credentials not configured. Email not sent.");
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
                    Subject = subject,
                    IsBodyHtml = true,
                    Body = htmlBody
                };
                message.To.Add(toEmail);

                // Add PDF attachment
                using var ms = new MemoryStream(attachmentData);
                var attachment = new Attachment(ms, attachmentName, "application/pdf");
                message.Attachments.Add(attachment);

                await client.SendMailAsync(message);
                _logger.LogInformation("Email with attachment sent to {Email} with subject: {Subject}", toEmail, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email with attachment to {Email}", toEmail);
                throw new InvalidOperationException("Failed to send email. Please try again later.");
            }
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string resetLink, string userName)
        {
            var subject = "Password Reset Request - MessHub";
            var body = GeneratePasswordResetEmailBody(userName, resetLink);
            await SendEmailAsync(toEmail, subject, body);
            _logger.LogInformation("Password reset email sent to {Email}", toEmail);
        }

        public async Task SendWelcomeEmailAsync(string toEmail, string userName, string fullName, string tempPassword)
        {
            var subject = "Welcome to MessHub - Your Account Has Been Created!";
            var body = GenerateWelcomeEmailBody(fullName, userName, tempPassword);
            await SendEmailAsync(toEmail, subject, body);
            _logger.LogInformation("Welcome email sent to {Email}", toEmail);
        }

        public async Task SendAccountStatusEmailAsync(string toEmail, string fullName, bool isActivated)
        {
            var subject = isActivated 
                ? "Account Activated - MessHub" 
                : "Account Deactivated - MessHub";
            var body = GenerateAccountStatusEmailBody(fullName, isActivated);
            await SendEmailAsync(toEmail, subject, body);
            _logger.LogInformation("Account status email sent to {Email}, Activated: {IsActivated}", toEmail, isActivated);
        }

        public async Task SendPaymentApprovalEmailAsync(string toEmail, string fullName, decimal amount, string paymentMethod, string periodName, DateTime approvedAt)
        {
            var subject = "Payment Approved - MessHub";
            var body = GeneratePaymentApprovalEmailBody(fullName, amount, paymentMethod, periodName, approvedAt);
            await SendEmailAsync(toEmail, subject, body);
            _logger.LogInformation("Payment approval email sent to {Email}", toEmail);
        }

        public async Task SendPeriodBillStatementEmailAsync(string toEmail, string fullName, MessPeriod period,
            int breakfastCount, int lunchCount, int dinnerCount,
            decimal mealCharges, decimal waterCharges, int teaCups, decimal teaCharges,
            decimal totalCharges, decimal totalPaid, decimal balance)
        {
            var subject = $"Bill Statement - {period.PeriodName} - MessHub";
            var body = GenerateBillStatementEmailBody(fullName, period, breakfastCount, lunchCount, dinnerCount,
                mealCharges, waterCharges, teaCups, teaCharges, totalCharges, totalPaid, balance);
            
            // Generate PDF
            var pdfData = GenerateBillStatementPdf(fullName, period, breakfastCount, lunchCount, dinnerCount,
                mealCharges, waterCharges, teaCups, teaCharges, totalCharges, totalPaid, balance);
            
            var attachmentName = $"BillStatement_{period.PeriodName.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.pdf";
            await SendEmailWithAttachmentAsync(toEmail, subject, body, pdfData, attachmentName);
            _logger.LogInformation("Bill statement email with PDF sent to {Email} for period {Period}", toEmail, period.PeriodName);
        }

        private static string GeneratePasswordResetEmailBody(string userName, string resetLink)
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

        private static string GenerateWelcomeEmailBody(string fullName, string userName, string tempPassword)
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
        <div style='background: linear-gradient(135deg, #10b981 0%, #059669 100%); padding: 30px; border-radius: 16px 16px 0 0; text-align: center;'>
            <h1 style='color: white; margin: 0; font-size: 28px;'>🍽️ MessHub</h1>
            <p style='color: rgba(255,255,255,0.9); margin: 10px 0 0 0;'>Welcome to the Family!</p>
        </div>
        <div style='background: white; padding: 40px 30px; border-radius: 0 0 16px 16px; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
            <h2 style='color: #1f2937; margin: 0 0 20px 0;'>Hello {fullName},</h2>
            <p style='color: #4b5563; margin: 0 0 20px 0;'>
                Welcome to MessHub! Your account has been successfully created. You can now log in and start using our mess management system.
            </p>
            <div style='background: #f0fdf4; border: 1px solid #bbf7d0; border-radius: 8px; padding: 20px; margin: 20px 0;'>
                <h3 style='color: #166534; margin: 0 0 15px 0; font-size: 16px;'>📋 Your Login Credentials</h3>
                <p style='color: #166534; margin: 5px 0;'><strong>Username:</strong> {userName}</p>
                <p style='color: #166534; margin: 5px 0;'><strong>Temporary Password:</strong> {tempPassword}</p>
            </div>
            <div style='background: #fef3c7; border: 1px solid #fcd34d; border-radius: 8px; padding: 15px; margin: 20px 0;'>
                <p style='color: #92400e; margin: 0; font-size: 14px;'>
                    ⚠️ <strong>Important:</strong> Please change your password after your first login for security purposes.
                </p>
            </div>
            <h3 style='color: #1f2937; margin: 25px 0 15px 0; font-size: 16px;'>What you can do with MessHub:</h3>
            <ul style='color: #4b5563; margin: 0; padding-left: 20px;'>
                <li style='margin: 8px 0;'>View your daily meal attendance</li>
                <li style='margin: 8px 0;'>Verify your attendance records</li>
                <li style='margin: 8px 0;'>Track your payments and dues</li>
                <li style='margin: 8px 0;'>Submit payment receipts</li>
                <li style='margin: 8px 0;'>View your billing history</li>
            </ul>
            <hr style='border: none; border-top: 1px solid #e5e7eb; margin: 30px 0;'>
            <p style='color: #6b7280; font-size: 14px; margin: 0; text-align: center;'>
                If you have any questions, please contact the mess administrator.
            </p>
        </div>
        <p style='color: #9ca3af; font-size: 12px; text-align: center; margin: 20px 0 0 0;'>
            © 2026 MessHub. All rights reserved.
        </p>
    </div>
</body>
</html>";
        }

        private static string GenerateAccountStatusEmailBody(string fullName, bool isActivated)
        {
            var statusColor = isActivated ? "#10b981" : "#ef4444";
            var statusGradient = isActivated 
                ? "linear-gradient(135deg, #10b981 0%, #059669 100%)" 
                : "linear-gradient(135deg, #ef4444 0%, #dc2626 100%)";
            var statusIcon = isActivated ? "✅" : "⚠️";
            var statusText = isActivated ? "Account Activated" : "Account Deactivated";
            var statusMessage = isActivated 
                ? "Great news! Your MessHub account has been activated. You can now log in and access all features."
                : "Your MessHub account has been deactivated. If you believe this is a mistake or have any questions, please contact the mess administrator.";

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, ""Helvetica Neue"", Arial, sans-serif; line-height: 1.6; margin: 0; padding: 0; background-color: #f4f4f5;'>
    <div style='max-width: 600px; margin: 0 auto; padding: 40px 20px;'>
        <div style='background: {statusGradient}; padding: 30px; border-radius: 16px 16px 0 0; text-align: center;'>
            <h1 style='color: white; margin: 0; font-size: 28px;'>🍽️ MessHub</h1>
            <p style='color: rgba(255,255,255,0.9); margin: 10px 0 0 0;'>{statusIcon} {statusText}</p>
        </div>
        <div style='background: white; padding: 40px 30px; border-radius: 0 0 16px 16px; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
            <h2 style='color: #1f2937; margin: 0 0 20px 0;'>Hello {fullName},</h2>
            <p style='color: #4b5563; margin: 0 0 20px 0;'>
                {statusMessage}
            </p>
            {(isActivated ? @"
            <div style='background: #f0fdf4; border: 1px solid #bbf7d0; border-radius: 8px; padding: 20px; margin: 20px 0;'>
                <p style='color: #166534; margin: 0;'>
                    🎉 You can now log in to MessHub and:
                </p>
                <ul style='color: #166534; margin: 10px 0 0 0; padding-left: 20px;'>
                    <li>View your attendance records</li>
                    <li>Track your payments</li>
                    <li>Submit payment receipts</li>
                </ul>
            </div>" : @"
            <div style='background: #fef2f2; border: 1px solid #fecaca; border-radius: 8px; padding: 20px; margin: 20px 0;'>
                <p style='color: #991b1b; margin: 0;'>
                    Your access to MessHub has been temporarily suspended. Please reach out to the administrator if you need assistance.
                </p>
            </div>")}
            <hr style='border: none; border-top: 1px solid #e5e7eb; margin: 30px 0;'>
            <p style='color: #6b7280; font-size: 14px; margin: 0; text-align: center;'>
                If you have any questions, please contact the mess administrator.
            </p>
        </div>
        <p style='color: #9ca3af; font-size: 12px; text-align: center; margin: 20px 0 0 0;'>
            © 2026 MessHub. All rights reserved.
        </p>
    </div>
</body>
</html>";
        }

        private static string GeneratePaymentApprovalEmailBody(string fullName, decimal amount, string paymentMethod, string periodName, DateTime approvedAt)
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
        <div style='background: linear-gradient(135deg, #10b981 0%, #059669 100%); padding: 30px; border-radius: 16px 16px 0 0; text-align: center;'>
            <h1 style='color: white; margin: 0; font-size: 28px;'>🍽️ MessHub</h1>
            <p style='color: rgba(255,255,255,0.9); margin: 10px 0 0 0;'>✅ Payment Approved</p>
        </div>
        <div style='background: white; padding: 40px 30px; border-radius: 0 0 16px 16px; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
            <h2 style='color: #1f2937; margin: 0 0 20px 0;'>Hello {fullName},</h2>
            <p style='color: #4b5563; margin: 0 0 20px 0;'>
                Great news! Your payment has been approved and credited to your account.
            </p>
            <div style='background: #f0fdf4; border: 1px solid #bbf7d0; border-radius: 8px; padding: 20px; margin: 20px 0;'>
                <h3 style='color: #166534; margin: 0 0 15px 0; font-size: 16px;'>💰 Payment Details</h3>
                <table style='width: 100%; color: #166534;'>
                    <tr>
                        <td style='padding: 5px 0;'><strong>Amount:</strong></td>
                        <td style='padding: 5px 0; text-align: right;'>Rs. {amount:N0}</td>
                    </tr>
                    <tr>
                        <td style='padding: 5px 0;'><strong>Payment Method:</strong></td>
                        <td style='padding: 5px 0; text-align: right;'>{paymentMethod}</td>
                    </tr>
                    <tr>
                        <td style='padding: 5px 0;'><strong>Period:</strong></td>
                        <td style='padding: 5px 0; text-align: right;'>{periodName}</td>
                    </tr>
                    <tr>
                        <td style='padding: 5px 0;'><strong>Approved On:</strong></td>
                        <td style='padding: 5px 0; text-align: right;'>{approvedAt:MMM dd, yyyy hh:mm tt}</td>
                    </tr>
                </table>
            </div>
            <p style='color: #4b5563; margin: 20px 0;'>
                You can view your updated balance and payment history in your MessHub dashboard.
            </p>
            <hr style='border: none; border-top: 1px solid #e5e7eb; margin: 30px 0;'>
            <p style='color: #6b7280; font-size: 14px; margin: 0; text-align: center;'>
                Thank you for your payment!
            </p>
        </div>
        <p style='color: #9ca3af; font-size: 12px; text-align: center; margin: 20px 0 0 0;'>
            © 2026 MessHub. All rights reserved.
        </p>
    </div>
</body>
</html>";
        }

        private static string GenerateBillStatementEmailBody(string fullName, MessPeriod period,
            int breakfastCount, int lunchCount, int dinnerCount,
            decimal mealCharges, decimal waterCharges, int teaCups, decimal teaCharges,
            decimal totalCharges, decimal totalPaid, decimal balance)
        {
            var balanceColor = balance > 0 ? "#dc2626" : "#059669";
            var balanceText = balance > 0 ? $"Rs. {balance:N0} Due" : (balance < 0 ? $"Rs. {Math.Abs(balance):N0} Credit" : "Settled");
            var balanceBg = balance > 0 ? "#fef2f2" : "#f0fdf4";
            var balanceBorder = balance > 0 ? "#fecaca" : "#bbf7d0";

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
            <p style='color: rgba(255,255,255,0.9); margin: 10px 0 0 0;'>📄 Bill Statement</p>
        </div>
        <div style='background: white; padding: 40px 30px; border-radius: 0 0 16px 16px; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
            <h2 style='color: #1f2937; margin: 0 0 20px 0;'>Hello {fullName},</h2>
            <p style='color: #4b5563; margin: 0 0 10px 0;'>
                Here is your bill statement for <strong>{period.PeriodName}</strong>
            </p>
            <p style='color: #6b7280; font-size: 14px; margin: 0 0 20px 0;'>
                Period: {period.StartDate:MMM dd, yyyy} - {period.EndDate:MMM dd, yyyy}
            </p>

            <div style='background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 20px; margin: 20px 0;'>
                <h3 style='color: #334155; margin: 0 0 15px 0; font-size: 16px;'>🍽️ Meal Summary</h3>
                <table style='width: 100%; color: #475569;'>
                    <tr>
                        <td style='padding: 5px 0;'>Breakfast ({breakfastCount} meals)</td>
                        <td style='padding: 5px 0; text-align: right;'>-</td>
                    </tr>
                    <tr>
                        <td style='padding: 5px 0;'>Lunch ({lunchCount} meals)</td>
                        <td style='padding: 5px 0; text-align: right;'>-</td>
                    </tr>
                    <tr>
                        <td style='padding: 5px 0;'>Dinner ({dinnerCount} meals)</td>
                        <td style='padding: 5px 0; text-align: right;'>-</td>
                    </tr>
                    <tr style='border-top: 1px solid #e2e8f0;'>
                        <td style='padding: 10px 0 5px 0;'><strong>Total Meal Charges</strong></td>
                        <td style='padding: 10px 0 5px 0; text-align: right;'><strong>Rs. {mealCharges:N0}</strong></td>
                    </tr>
                </table>
            </div>

            <div style='background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 20px; margin: 20px 0;'>
                <h3 style='color: #334155; margin: 0 0 15px 0; font-size: 16px;'>📋 Other Charges</h3>
                <table style='width: 100%; color: #475569;'>
                    <tr>
                        <td style='padding: 5px 0;'>Water Charges</td>
                        <td style='padding: 5px 0; text-align: right;'>Rs. {waterCharges:N0}</td>
                    </tr>
                    <tr>
                        <td style='padding: 5px 0;'>Tea ({teaCups} cups @ Rs. {period.TeaPricePerCup}/cup)</td>
                        <td style='padding: 5px 0; text-align: right;'>Rs. {teaCharges:N0}</td>
                    </tr>
                </table>
            </div>

            <div style='background: #eff6ff; border: 1px solid #bfdbfe; border-radius: 8px; padding: 20px; margin: 20px 0;'>
                <table style='width: 100%; color: #1e40af;'>
                    <tr>
                        <td style='padding: 5px 0; font-size: 18px;'><strong>Grand Total</strong></td>
                        <td style='padding: 5px 0; text-align: right; font-size: 18px;'><strong>Rs. {totalCharges:N0}</strong></td>
                    </tr>
                    <tr>
                        <td style='padding: 5px 0;'>Total Paid</td>
                        <td style='padding: 5px 0; text-align: right;'>Rs. {totalPaid:N0}</td>
                    </tr>
                </table>
            </div>

            <div style='background: {balanceBg}; border: 1px solid {balanceBorder}; border-radius: 8px; padding: 20px; margin: 20px 0; text-align: center;'>
                <p style='color: {balanceColor}; margin: 0; font-size: 20px; font-weight: bold;'>
                    {balanceText}
                </p>
            </div>

            <p style='color: #4b5563; margin: 20px 0;'>
                📎 A detailed PDF statement is attached to this email for your records.
            </p>
            <hr style='border: none; border-top: 1px solid #e5e7eb; margin: 30px 0;'>
            <p style='color: #6b7280; font-size: 14px; margin: 0; text-align: center;'>
                If you have any questions about this bill, please contact the mess administrator.
            </p>
        </div>
        <p style='color: #9ca3af; font-size: 12px; text-align: center; margin: 20px 0 0 0;'>
            © 2026 MessHub. All rights reserved.
        </p>
    </div>
</body>
</html>";
        }

        private static byte[] GenerateBillStatementPdf(string fullName, MessPeriod period,
            int breakfastCount, int lunchCount, int dinnerCount,
            decimal mealCharges, decimal waterCharges, int teaCups, decimal teaCharges,
            decimal totalCharges, decimal totalPaid, decimal balance)
        {
            var document = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(QuestPDF.Helpers.PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("MessHub").FontSize(24).Bold().FontColor(QuestPDF.Helpers.Colors.Blue.Darken2);
                                c.Item().Text("Bill Statement").FontSize(14).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                            });
                            row.RelativeItem().AlignRight().Column(c =>
                            {
                                c.Item().Text($"Date: {DateTime.Now:MMM dd, yyyy}").FontSize(10);
                                c.Item().Text($"Statement #: {DateTime.Now:yyyyMMddHHmmss}").FontSize(10);
                            });
                        });
                        col.Item().PaddingVertical(15).LineHorizontal(2).LineColor(QuestPDF.Helpers.Colors.Blue.Darken2);
                    });

                    page.Content().Column(col =>
                    {
                        // Member Info
                        col.Item().PaddingBottom(20).Column(c =>
                        {
                            c.Item().Text("Bill To:").Bold().FontSize(12);
                            c.Item().Text(fullName).FontSize(14).Bold();
                        });

                        // Period Info
                        col.Item().PaddingBottom(20).Background(QuestPDF.Helpers.Colors.Grey.Lighten4).Padding(15).Column(c =>
                        {
                            c.Item().Text($"Period: {period.PeriodName}").Bold().FontSize(12);
                            c.Item().Text($"Duration: {period.StartDate:MMM dd, yyyy} - {period.EndDate:MMM dd, yyyy}");
                        });

                        // Charges Table
                        col.Item().PaddingBottom(20).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(4);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(2);
                            });

                            // Header
                            table.Header(header =>
                            {
                                header.Cell().Background(QuestPDF.Helpers.Colors.Blue.Darken2).Padding(8).Text("Description").Bold().FontColor(QuestPDF.Helpers.Colors.White);
                                header.Cell().Background(QuestPDF.Helpers.Colors.Blue.Darken2).Padding(8).AlignCenter().Text("Qty").Bold().FontColor(QuestPDF.Helpers.Colors.White);
                                header.Cell().Background(QuestPDF.Helpers.Colors.Blue.Darken2).Padding(8).AlignRight().Text("Amount (Rs.)").Bold().FontColor(QuestPDF.Helpers.Colors.White);
                            });

                            // Meals
                            table.Cell().BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(8).Text("Breakfast");
                            table.Cell().BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(8).AlignCenter().Text(breakfastCount.ToString());
                            table.Cell().BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(8).AlignRight().Text("-");

                            table.Cell().BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(8).Text("Lunch");
                            table.Cell().BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(8).AlignCenter().Text(lunchCount.ToString());
                            table.Cell().BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(8).AlignRight().Text("-");

                            table.Cell().BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(8).Text("Dinner");
                            table.Cell().BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(8).AlignCenter().Text(dinnerCount.ToString());
                            table.Cell().BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(8).AlignRight().Text("-");

                            table.Cell().Background(QuestPDF.Helpers.Colors.Grey.Lighten4).Padding(8).Text("Total Meal Charges").Bold();
                            table.Cell().Background(QuestPDF.Helpers.Colors.Grey.Lighten4).Padding(8).Text("");
                            table.Cell().Background(QuestPDF.Helpers.Colors.Grey.Lighten4).Padding(8).AlignRight().Text($"{mealCharges:N0}").Bold();

                            // Water
                            table.Cell().BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(8).Text("Water Charges (Fixed)");
                            table.Cell().BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(8).AlignCenter().Text("1");
                            table.Cell().BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(8).AlignRight().Text($"{waterCharges:N0}");

                            // Tea
                            table.Cell().BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(8).Text($"Tea @ Rs. {period.TeaPricePerCup}/cup");
                            table.Cell().BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(8).AlignCenter().Text(teaCups.ToString());
                            table.Cell().BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(8).AlignRight().Text($"{teaCharges:N0}");
                        });

                        // Summary
                        col.Item().AlignRight().Width(250).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                            });

                            table.Cell().Padding(5).Text("Grand Total:").Bold();
                            table.Cell().Padding(5).AlignRight().Text($"Rs. {totalCharges:N0}").Bold();

                            table.Cell().Padding(5).Text("Total Paid:");
                            table.Cell().Padding(5).AlignRight().Text($"Rs. {totalPaid:N0}");

                            var balanceColor = balance > 0 ? QuestPDF.Helpers.Colors.Red.Darken1 : QuestPDF.Helpers.Colors.Green.Darken1;
                            var balanceText = balance > 0 ? $"Rs. {balance:N0} Due" : (balance < 0 ? $"Rs. {Math.Abs(balance):N0} Credit" : "Settled");
                            
                            table.Cell().Background(balanceColor).Padding(8).Text("Balance:").Bold().FontColor(QuestPDF.Helpers.Colors.White);
                            table.Cell().Background(balanceColor).Padding(8).AlignRight().Text(balanceText).Bold().FontColor(QuestPDF.Helpers.Colors.White);
                        });
                    });

                    page.Footer().AlignCenter().Column(col =>
                    {
                        col.Item().PaddingTop(20).LineHorizontal(1).LineColor(QuestPDF.Helpers.Colors.Grey.Lighten1);
                        col.Item().PaddingTop(10).Text("Thank you for being a member of MessHub!").FontSize(10).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                        col.Item().Text("© 2026 MessHub. All rights reserved.").FontSize(9).FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                    });
                });
            });

            return document.GeneratePdf();
        }
    }
}
