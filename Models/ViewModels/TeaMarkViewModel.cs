using System.ComponentModel.DataAnnotations;

namespace MessManagement.Models.ViewModels
{
    /// <summary>
    /// Calendar day data for tea overview
    /// </summary>
    public class TeaCalendarDayData
    {
        public int TotalCups { get; set; }
        public int MembersWithTea { get; set; }
        public int VerifiedCount { get; set; }
        public int TotalEntries { get; set; }
        public bool HasData { get; set; }
    }

    /// <summary>
    /// ViewModel for the Admin "Mark Tea" page - marks cups for all members on a date
    /// </summary>
    public class TeaMarkViewModel
    {
        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Date")]
        public DateTime Date { get; set; } = DateTime.Today;

        [Display(Name = "Period")]
        public int? PeriodId { get; set; }

        public string? PeriodName { get; set; }

        public List<TeaMarkItemViewModel> Items { get; set; } = new();
    }

    /// <summary>
    /// Individual member's tea entry for a specific date
    /// </summary>
    public class TeaMarkItemViewModel
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;

        [Range(0, 100, ErrorMessage = "Cups must be between 0 and 100")]
        public int Cups { get; set; } = 0;

        [StringLength(255)]
        public string? Remarks { get; set; }

        /// <summary>
        /// Whether member has verified this entry
        /// </summary>
        public bool VerifiedByUser { get; set; }

        /// <summary>
        /// Existing TeaEntryId if updating, null if new
        /// </summary>
        public int? TeaEntryId { get; set; }

        /// <summary>
        /// Whether member had any meal attendance on this day (for reference)
        /// </summary>
        public bool HasMealAttendance { get; set; }
    }

    /// <summary>
    /// Summary stats for the Tea Index page
    /// </summary>
    public class TeaSummaryViewModel
    {
        public MessPeriod? CurrentPeriod { get; set; }
        public List<MessPeriod> AllPeriods { get; set; } = new();

        public int TotalCups { get; set; }
        public decimal TotalTeaCost { get; set; }
        public int TotalEntries { get; set; }
        public int VerifiedEntries { get; set; }
        public int PendingVerification { get; set; }

        /// <summary>
        /// Daily breakdown for the period
        /// </summary>
        public List<TeaDailySummary> DailyBreakdown { get; set; } = new();

        /// <summary>
        /// Per-member totals for the period
        /// </summary>
        public List<TeaMemberSummary> MemberTotals { get; set; } = new();
    }

    /// <summary>
    /// Daily summary row for Tea Index
    /// </summary>
    public class TeaDailySummary
    {
        public DateTime Date { get; set; }
        public int TotalCups { get; set; }
        public int MembersWithTea { get; set; }
        public int VerifiedCount { get; set; }
        public int TotalEntries { get; set; }
        public decimal DayCost { get; set; }
    }

    /// <summary>
    /// Per-member summary for reports
    /// </summary>
    public class TeaMemberSummary
    {
        public User? User { get; set; }
        public int TotalCups { get; set; }
        public decimal TotalCharge { get; set; }
        public int VerifiedCount { get; set; }
        public int TotalEntries { get; set; }
    }
}
