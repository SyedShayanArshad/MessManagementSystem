using System.ComponentModel.DataAnnotations;

namespace MessManagement.Models
{
    public class TeaRecord
    {
        [Key]
        public int TeaRecordId { get; set; }

        [Display(Name = "Period")]
        public int? PeriodId { get; set; }

        [Required(ErrorMessage = "Date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Date")]
        public DateTime Date { get; set; }

        [Required(ErrorMessage = "Total cups served is required")]
        [Range(0, 10000, ErrorMessage = "Cups served must be between 0 and 10,000")]
        [Display(Name = "Cups Served")]
        public int TotalCupsServed { get; set; } = 0;

        [StringLength(255)]
        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }

        public MessPeriod? MessPeriod { get; set; }
    }
}
