using MessManagement.Models;

namespace MessManagement.Services
{
    public interface IEmailService
    {
        Task SendPasswordResetEmailAsync(string toEmail, string resetLink, string userName);
        
        /// <summary>
        /// Sends a welcome email when a new user account is created
        /// </summary>
        Task SendWelcomeEmailAsync(string toEmail, string userName, string fullName, string tempPassword);
        
        /// <summary>
        /// Sends notification when user account is activated or deactivated
        /// </summary>
        Task SendAccountStatusEmailAsync(string toEmail, string fullName, bool isActivated);
        
        /// <summary>
        /// Sends notification when payment is approved by admin
        /// </summary>
        Task SendPaymentApprovalEmailAsync(string toEmail, string fullName, decimal amount, string paymentMethod, string periodName, DateTime approvedAt);
        
        /// <summary>
        /// Sends period-end bill statement with PDF attachment
        /// </summary>
        Task SendPeriodBillStatementEmailAsync(string toEmail, string fullName, MessPeriod period, 
            int breakfastCount, int lunchCount, int dinnerCount, 
            decimal mealCharges, decimal waterCharges, int teaCups, decimal teaCharges, 
            decimal totalCharges, decimal totalPaid, decimal balance);
    }
}
