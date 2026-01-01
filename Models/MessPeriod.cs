using System.ComponentModel.DataAnnotations;

namespace MessManagement.Models
{
    public class MessPeriod
    {
        [Key]
        public int PeriodId { get; set; }

        [Required(ErrorMessage = "Period name is required")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Period name must be between 3 and 50 characters")]
        [Display(Name = "Period Name")]
        public string PeriodName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Start date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "End date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = false;

        [Range(0, 100000, ErrorMessage = "Water charge must be between 0 and 100,000")]
        [Display(Name = "Fixed Water Charge")]
        [DisplayFormat(DataFormatString = "{0:N0}", ApplyFormatInEditMode = false)]
        public decimal FixedWaterCharge { get; set; }

        [Range(0, 1000, ErrorMessage = "Tea price must be between 0 and 1,000")]
        [Display(Name = "Tea Price Per Cup")]
        [DisplayFormat(DataFormatString = "{0:N0}", ApplyFormatInEditMode = false)]
        public decimal TeaPricePerCup { get; set; }

        public ICollection<Attendance>? Attendances { get; set; }
        public ICollection<TeaRecord>? TeaRecords { get; set; }
        public ICollection<TeaEntry>? TeaEntries { get; set; }
        public ICollection<Payment>? Payments { get; set; }
    }
}
