using System.ComponentModel.DataAnnotations;

namespace MessManagement.Models
{
    public enum PaymentStatus
    {
        Pending,    // User submitted, awaiting admin verification
        Approved,   // Admin verified and approved
        Rejected,   // Admin rejected (with reason)
        Completed   // Auto for Stripe (webhook verified)
    }

    public class Payment
    {
        [Key]
        public int PaymentId { get; set; }

        [Required(ErrorMessage = "Member is required")]
        [Display(Name = "Member")]
        public int UserId { get; set; }

        [Display(Name = "Period")]
        public int? PeriodId { get; set; }

        [Required(ErrorMessage = "Amount is required")]
        [Range(1, 1000000, ErrorMessage = "Amount must be between Rs. 1 and Rs. 1,000,000")]
        [Display(Name = "Amount (Rs.)")]
        [DisplayFormat(DataFormatString = "{0:N0}", ApplyFormatInEditMode = false)]
        public decimal Amount { get; set; }

        [Required]
        [DataType(DataType.DateTime)]
        [Display(Name = "Payment Date")]
        public DateTime PaymentDate { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "Payment method is required")]
        [StringLength(50)]
        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } = "Cash"; // Cash / Stripe / Bank Transfer / Other

        [StringLength(255)]
        [Display(Name = "Stripe Payment ID")]
        public string? StripePaymentId { get; set; }

        [StringLength(255)]
        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }

        // Payment Approval Workflow Fields
        [Display(Name = "Status")]
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

        [StringLength(100)]
        [Display(Name = "Reference Number")]
        public string? ReferenceNumber { get; set; } // Bank transaction ID, receipt number, etc.

        [Display(Name = "Approved By")]
        public int? ApprovedByUserId { get; set; }
        public User? ApprovedByUser { get; set; }

        [DataType(DataType.DateTime)]
        [Display(Name = "Approved Date")]
        public DateTime? ApprovedAt { get; set; }

        [StringLength(500)]
        [Display(Name = "Rejection Reason")]
        public string? RejectionReason { get; set; }

        [Display(Name = "Attendance")]
        public int? AttendanceId { get; set; }
        public Attendance? Attendance { get; set; }

        public User? User { get; set; }
        public MessPeriod? MessPeriod { get; set; }

        // Helper properties
        public bool IsPending => Status == PaymentStatus.Pending;
        public bool IsApproved => Status == PaymentStatus.Approved || Status == PaymentStatus.Completed;
        public bool IsRejected => Status == PaymentStatus.Rejected;

        public string StatusBadgeClass => Status switch
        {
            PaymentStatus.Pending => "badge-warning",
            PaymentStatus.Approved => "badge-success",
            PaymentStatus.Completed => "badge-success",
            PaymentStatus.Rejected => "badge-error",
            _ => "badge-secondary"
        };

        public string StatusDisplayText => Status switch
        {
            PaymentStatus.Pending => "Pending Approval",
            PaymentStatus.Approved => "Approved",
            PaymentStatus.Completed => "Completed",
            PaymentStatus.Rejected => "Rejected",
            _ => "Unknown"
        };
    }
}
