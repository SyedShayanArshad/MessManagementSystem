using System.ComponentModel.DataAnnotations;

namespace MessManagement.Models
{
    public class DishPlan
    {
        [Key]
        public int DishPlanId { get; set; }

        [Required(ErrorMessage = "Day of week is required")]
        [StringLength(20)]
        [Display(Name = "Day")]
        public string DayOfWeek { get; set; } = string.Empty;

        [Required(ErrorMessage = "Meal type is required")]
        [StringLength(20)]
        [Display(Name = "Meal Type")]
        public string MealType { get; set; } = string.Empty; // Breakfast / Lunch / Dinner

        [Required(ErrorMessage = "Dish name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Dish name must be between 2 and 100 characters")]
        [Display(Name = "Dish Name")]
        public string DishName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Price is required")]
        [Range(1, 100000, ErrorMessage = "Price must be between Rs. 1 and Rs. 100,000")]
        [Display(Name = "Price (PKR)")]
        [DisplayFormat(DataFormatString = "{0:N0}", ApplyFormatInEditMode = false)]
        public decimal Price { get; set; }

        [StringLength(255)]
        public string? Notes { get; set; }

        public ICollection<Attendance>? Attendances { get; set; }
    }
}
