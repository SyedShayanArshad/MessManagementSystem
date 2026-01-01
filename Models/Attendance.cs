using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MessManagement.Models
{
    public class Attendance
    {
        [Key]
        public int AttendanceId { get; set; }

        [Required]
        public int UserId { get; set; }

        // Separate dish plans for each meal type
        public int? BreakfastDishPlanId { get; set; }
        public int? LunchDishPlanId { get; set; }
        public int? DinnerDishPlanId { get; set; }

        // Legacy field - kept for backward compatibility
        public int? DishPlanId { get; set; }

        public int? PeriodId { get; set; }

        [Required]
        public DateTime Date { get; set; }

        // Separate presence tracking for each meal
        public bool IsBreakfastPresent { get; set; } = false;
        public bool IsLunchPresent { get; set; } = true;
        public bool IsDinnerPresent { get; set; } = true;

        // Legacy field - kept for backward compatibility
        public bool IsPresent { get; set; } = true;

        // Separate verification for each meal type
        public bool BreakfastVerified { get; set; } = false;
        public bool LunchVerified { get; set; } = false;
        public bool DinnerVerified { get; set; } = false;
        public DateTime? BreakfastVerifiedOn { get; set; }
        public DateTime? LunchVerifiedOn { get; set; }
        public DateTime? DinnerVerifiedOn { get; set; }

        // Legacy single verification field - now computed from individual verifications
        public bool VerifiedByUser { get; set; } = false;

        public DateTime? VerifiedOn { get; set; }

        /// <summary>
        /// Returns true if all present meals are verified
        /// </summary>
        [NotMapped]
        public bool AllMealsVerified => 
            (!IsBreakfastPresent || BreakfastVerified) &&
            (!IsLunchPresent || LunchVerified) &&
            (!IsDinnerPresent || DinnerVerified);

        /// <summary>
        /// Count of meals that need verification
        /// </summary>
        [NotMapped]
        public int PendingVerificationCount =>
            (IsBreakfastPresent && !BreakfastVerified ? 1 : 0) +
            (IsLunchPresent && !LunchVerified ? 1 : 0) +
            (IsDinnerPresent && !DinnerVerified ? 1 : 0);

        // Computed property for total charge
        [NotMapped]
        public decimal TotalCharge { get; set; }

        // Navigation
        public User? User { get; set; }
        
        [ForeignKey("BreakfastDishPlanId")]
        public DishPlan? BreakfastDishPlan { get; set; }
        
        [ForeignKey("LunchDishPlanId")]
        public DishPlan? LunchDishPlan { get; set; }
        
        [ForeignKey("DinnerDishPlanId")]
        public DishPlan? DinnerDishPlan { get; set; }
        
        [ForeignKey("DishPlanId")]
        public DishPlan? DishPlan { get; set; }
        
        public MessPeriod? MessPeriod { get; set; }
        public List<Payment>? Payments { get; set; }
    }
}
