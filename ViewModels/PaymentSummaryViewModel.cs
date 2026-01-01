using MessManagement.Models;

namespace MessManagement.ViewModels
{
    public class PaymentSummaryViewModel
    {
        public MessPeriod? CurrentPeriod { get; set; }
        public List<MessPeriod> AllPeriods { get; set; } = new();
        public List<MemberPaymentSummary> MemberSummaries { get; set; } = new();
        
        // Period totals
        public decimal TotalPeriodCharges { get; set; }
        public decimal TotalPeriodPaid { get; set; }
        public decimal TotalPeriodRemaining => TotalPeriodCharges - TotalPeriodPaid;
        public decimal CollectionPercentage => TotalPeriodCharges > 0 ? (TotalPeriodPaid / TotalPeriodCharges) * 100 : 0;
        
        // Payment method breakdown
        public decimal TotalCashPayments { get; set; }
        public decimal TotalStripePayments { get; set; }
        public decimal TotalBankTransferPayments { get; set; }
        public decimal TotalOtherPayments { get; set; }
        
        public int TotalPaymentCount { get; set; }
    }

    public class MemberPaymentSummary
    {
        public User? User { get; set; }
        
        // Charges breakdown
        public decimal MealCharges { get; set; }
        public decimal WaterCharges { get; set; }
        public decimal TeaCharges { get; set; }
        public decimal TotalCharges => MealCharges + WaterCharges + TeaCharges;
        
        // Payment info
        public decimal TotalPaid { get; set; }
        public decimal RemainingBalance => TotalCharges - TotalPaid;
        public decimal PaymentPercentage => TotalCharges > 0 ? (TotalPaid / TotalCharges) * 100 : 0;
        
        // Attendance summary
        public int BreakfastCount { get; set; }
        public int LunchCount { get; set; }
        public int DinnerCount { get; set; }
        public int TeaCups { get; set; }
        
        // Payment records for this period
        public List<Payment> Payments { get; set; } = new();
        
        // Status
        public string PaymentStatus => RemainingBalance <= 0 ? "Paid" : (TotalPaid > 0 ? "Partial" : "Unpaid");
    }

    public class MemberPaymentDetailViewModel
    {
        public User? User { get; set; }
        public MessPeriod? Period { get; set; }
        public List<MessPeriod> AllPeriods { get; set; } = new();
        
        // Charges
        public decimal MealCharges { get; set; }
        public decimal WaterCharges { get; set; }
        public decimal TeaCharges { get; set; }
        public decimal TotalCharges => MealCharges + WaterCharges + TeaCharges;
        
        // Payments
        public decimal TotalPaid { get; set; }
        public decimal RemainingBalance => TotalCharges - TotalPaid;
        
        // Attendance details
        public List<AttendanceChargeDetail> AttendanceDetails { get; set; } = new();
        public int TeaCups { get; set; }
        
        // Payment history
        public List<Payment> PaymentHistory { get; set; } = new();
    }

    public class AttendanceChargeDetail
    {
        public DateTime Date { get; set; }
        public string? BreakfastDish { get; set; }
        public decimal BreakfastCharge { get; set; }
        public string? LunchDish { get; set; }
        public decimal LunchCharge { get; set; }
        public string? DinnerDish { get; set; }
        public decimal DinnerCharge { get; set; }
        public decimal DayTotal => BreakfastCharge + LunchCharge + DinnerCharge;
    }
}
