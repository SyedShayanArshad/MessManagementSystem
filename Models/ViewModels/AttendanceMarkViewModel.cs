using System.ComponentModel.DataAnnotations;
namespace MessManagement.Models.ViewModels
{
    /// <summary>
    /// Calendar day data for attendance overview
    /// </summary>
    public class CalendarDayData
    {
        public int Breakfast { get; set; }
        public int Lunch { get; set; }
        public int Dinner { get; set; }
        public int Total => Breakfast + Lunch + Dinner;
        public bool HasData { get; set; }
    }

    public class AttendanceItemViewModel
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        
        // Separate presence for each meal
        public bool IsBreakfastPresent { get; set; } = false;
        public bool IsLunchPresent { get; set; } = true;
        public bool IsDinnerPresent { get; set; } = true;
        
        // Legacy field
        public bool IsPresent { get; set; } = true;
        
        // Separate dish selection for each meal
        public int? BreakfastDishPlanId { get; set; }
        public int? LunchDishPlanId { get; set; }
        public int? DinnerDishPlanId { get; set; }
        
        // Legacy field
        public int? DishPlanId { get; set; }
        
        // Separate charges for each meal
        public decimal BreakfastCharge { get; set; }
        public decimal LunchCharge { get; set; }
        public decimal DinnerCharge { get; set; }
        public decimal TotalCharge => BreakfastCharge + LunchCharge + DinnerCharge;
        
        // Verification status for each meal
        public bool BreakfastVerified { get; set; }
        public bool LunchVerified { get; set; }
        public bool DinnerVerified { get; set; }
        
        // Legacy fields
        public bool AutoChargeExists { get; set; }
        public decimal? AutoChargeAmount { get; set; }
    }

    public class AttendanceMarkViewModel
    {
        [Required]
        public DateTime Date { get; set; } = DateTime.Today;
        public List<AttendanceItemViewModel> Items { get; set; } = new List<AttendanceItemViewModel>();
        
        // Summary totals
        public decimal TotalBreakfastCharges => Items.Sum(i => i.BreakfastCharge);
        public decimal TotalLunchCharges => Items.Sum(i => i.LunchCharge);
        public decimal TotalDinnerCharges => Items.Sum(i => i.DinnerCharge);
        public decimal GrandTotal => TotalBreakfastCharges + TotalLunchCharges + TotalDinnerCharges;
    }
}
