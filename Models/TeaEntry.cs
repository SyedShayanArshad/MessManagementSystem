using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MessManagement.Models
{
    /// <summary>
    /// Represents a tea consumption entry for a specific user on a specific date.
    /// Supports member verification similar to Attendance.
    /// </summary>
    public class TeaEntry
    {
        [Key]
        public int TeaEntryId { get; set; }

        [Required]
        [Display(Name = "Member")]
        public int UserId { get; set; }

        [Display(Name = "Period")]
        public int? PeriodId { get; set; }

        [Required(ErrorMessage = "Date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Date")]
        public DateTime Date { get; set; }

        [Required(ErrorMessage = "Number of cups is required")]
        [Range(0, 100, ErrorMessage = "Cups must be between 0 and 100")]
        [Display(Name = "Cups")]
        public int Cups { get; set; } = 0;

        [StringLength(255)]
        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }

        /// <summary>
        /// Whether the member has verified/confirmed this tea entry
        /// </summary>
        [Display(Name = "Verified by Member")]
        public bool VerifiedByUser { get; set; } = false;

        /// <summary>
        /// When the member verified this entry
        /// </summary>
        [Display(Name = "Verified On")]
        public DateTime? VerifiedOn { get; set; }

        /// <summary>
        /// Computed: Cups × TeaPricePerCup (set by controller)
        /// </summary>
        [NotMapped]
        public decimal TeaCharge { get; set; }

        // Navigation properties
        public User? User { get; set; }
        public MessPeriod? MessPeriod { get; set; }
    }
}
