using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MessManagement.Data;
using MessManagement.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MessManagement.Controllers
{
    [Authorize]
    public class AttendanceController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AttendanceController(ApplicationDbContext context) => _context = context;

        // Admin-only view to list and mark attendance
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index(DateTime? date, int? periodId)
        {
            var modelDate = date ?? DateTime.Today;
            
            var users = await _context.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
            var existing = await _context.Attendances.Where(a => a.Date == modelDate).ToListAsync();
            
            // Get dish plans filtered by the current day of week
            var dayOfWeek = modelDate.DayOfWeek.ToString();
            var dishPlans = await _context.DishPlans.Where(d => d.DayOfWeek == dayOfWeek).ToListAsync();
            
            var breakfastDishes = dishPlans.Where(d => d.MealType == "Breakfast")
                .Select(dp => new SelectListItem { Value = dp.DishPlanId.ToString(), Text = $"{dp.DishName} (Rs. {dp.Price})" }).ToList();
            var lunchDishes = dishPlans.Where(d => d.MealType == "Lunch")
                .Select(dp => new SelectListItem { Value = dp.DishPlanId.ToString(), Text = $"{dp.DishName} (Rs. {dp.Price})" }).ToList();
            var dinnerDishes = dishPlans.Where(d => d.MealType == "Dinner")
                .Select(dp => new SelectListItem { Value = dp.DishPlanId.ToString(), Text = $"{dp.DishName} (Rs. {dp.Price})" }).ToList();
            
            ViewBag.BreakfastDishes = breakfastDishes;
            ViewBag.LunchDishes = lunchDishes;
            ViewBag.DinnerDishes = dinnerDishes;
            
            // ============================================
            // DASHBOARD DATA
            // ============================================
            
            // Get all mess periods for period-based calendar
            var allPeriods = await _context.MessPeriods
                .OrderByDescending(p => p.StartDate)
                .ToListAsync();
            ViewBag.AllPeriods = allPeriods;
            
            // Current/Selected period
            var selectedPeriod = periodId.HasValue 
                ? allPeriods.FirstOrDefault(p => p.PeriodId == periodId.Value)
                : allPeriods.FirstOrDefault(p => p.StartDate <= DateTime.Today && p.EndDate >= DateTime.Today)
                  ?? allPeriods.FirstOrDefault();
            ViewBag.SelectedPeriod = selectedPeriod;
            
            // Period-based calendar data
            var periodCalendarData = new Dictionary<DateTime, MessManagement.Models.ViewModels.CalendarDayData>();
            if (selectedPeriod != null)
            {
                var periodAttendances = await _context.Attendances
                    .Where(a => a.Date >= selectedPeriod.StartDate && a.Date <= selectedPeriod.EndDate)
                    .ToListAsync();
                
                for (var d = selectedPeriod.StartDate; d <= selectedPeriod.EndDate; d = d.AddDays(1))
                {
                    var dayAttendances = periodAttendances.Where(a => a.Date == d).ToList();
                    periodCalendarData[d] = new MessManagement.Models.ViewModels.CalendarDayData
                    {
                        Breakfast = dayAttendances.Count(a => a.IsBreakfastPresent),
                        Lunch = dayAttendances.Count(a => a.IsLunchPresent),
                        Dinner = dayAttendances.Count(a => a.IsDinnerPresent),
                        HasData = dayAttendances.Any()
                    };
                }
            }
            ViewBag.PeriodCalendarData = periodCalendarData;
            
            // Today's attendance stats
            var todayAttendances = await _context.Attendances.Where(a => a.Date == DateTime.Today).ToListAsync();
            ViewBag.TodayBreakfastPresent = todayAttendances.Count(a => a.IsBreakfastPresent);
            ViewBag.TodayLunchPresent = todayAttendances.Count(a => a.IsLunchPresent);
            ViewBag.TodayDinnerPresent = todayAttendances.Count(a => a.IsDinnerPresent);
            ViewBag.TotalMembers = users.Count;
            
            // Current period (for display)
            var currentPeriod = allPeriods.FirstOrDefault(p => p.StartDate <= DateTime.Today && p.EndDate >= DateTime.Today);
            ViewBag.CurrentPeriod = currentPeriod;
            
            // Allow editing any date within current period
            var isEditable = currentPeriod != null && modelDate >= currentPeriod.StartDate && modelDate <= currentPeriod.EndDate;
            ViewBag.IsEditable = isEditable;
            
            // Pending verifications
            var pendingVerifications = await _context.Attendances
                .Where(a => (a.IsBreakfastPresent && !a.BreakfastVerified) ||
                           (a.IsLunchPresent && !a.LunchVerified) ||
                           (a.IsDinnerPresent && !a.DinnerVerified))
                .CountAsync();
            ViewBag.PendingVerifications = pendingVerifications;
            
            // Recent pending (for panel) - get most recent 10
            var recentPending = await _context.Attendances
                .Include(a => a.User)
                .Where(a => (a.IsBreakfastPresent && !a.BreakfastVerified) ||
                           (a.IsLunchPresent && !a.LunchVerified) ||
                           (a.IsDinnerPresent && !a.DinnerVerified))
                .OrderByDescending(a => a.Date)
                .Take(10)
                .ToListAsync();
            ViewBag.RecentPending = recentPending;
            
            // ============================================
            // ATTENDANCE MARK VIEW MODEL
            // ============================================
            
            var vm = new MessManagement.Models.ViewModels.AttendanceMarkViewModel { Date = modelDate };
            
            // Get default dish plans for auto-selection (first available dish of each type for today)
            var defaultBreakfastDish = dishPlans.FirstOrDefault(d => d.MealType == "Breakfast");
            var defaultLunchDish = dishPlans.FirstOrDefault(d => d.MealType == "Lunch");
            var defaultDinnerDish = dishPlans.FirstOrDefault(d => d.MealType == "Dinner");
            
            foreach (var user in users)
            {
                var att = existing.FirstOrDefault(e => e.UserId == user.UserId);
                
                // Calculate charges based on selected dishes
                decimal breakfastCharge = 0, lunchCharge = 0, dinnerCharge = 0;
                
                // Determine dish plan IDs - use existing if available, otherwise use default for the day
                int? breakfastDishId = att?.BreakfastDishPlanId ?? defaultBreakfastDish?.DishPlanId;
                int? lunchDishId = att?.LunchDishPlanId ?? defaultLunchDish?.DishPlanId;
                int? dinnerDishId = att?.DinnerDishPlanId ?? defaultDinnerDish?.DishPlanId;
                
                if (att != null)
                {
                    if (att.IsBreakfastPresent && att.BreakfastDishPlanId.HasValue)
                    {
                        var dish = dishPlans.FirstOrDefault(d => d.DishPlanId == att.BreakfastDishPlanId);
                        breakfastCharge = dish?.Price ?? 0;
                    }
                    if (att.IsLunchPresent && att.LunchDishPlanId.HasValue)
                    {
                        var dish = dishPlans.FirstOrDefault(d => d.DishPlanId == att.LunchDishPlanId);
                        lunchCharge = dish?.Price ?? 0;
                    }
                    if (att.IsDinnerPresent && att.DinnerDishPlanId.HasValue)
                    {
                        var dish = dishPlans.FirstOrDefault(d => d.DishPlanId == att.DinnerDishPlanId);
                        dinnerCharge = dish?.Price ?? 0;
                    }
                }
                
                vm.Items.Add(new MessManagement.Models.ViewModels.AttendanceItemViewModel
                {
                    UserId = user.UserId,
                    FullName = user.FullName,
                    IsBreakfastPresent = att?.IsBreakfastPresent ?? true,
                    IsLunchPresent = att?.IsLunchPresent ?? true,
                    IsDinnerPresent = att?.IsDinnerPresent ?? true,
                    BreakfastDishPlanId = breakfastDishId,
                    LunchDishPlanId = lunchDishId,
                    DinnerDishPlanId = dinnerDishId,
                    BreakfastCharge = breakfastCharge,
                    LunchCharge = lunchCharge,
                    DinnerCharge = dinnerCharge,
                    BreakfastVerified = att?.BreakfastVerified ?? false,
                    LunchVerified = att?.LunchVerified ?? false,
                    DinnerVerified = att?.DinnerVerified ?? false,
                    HasAttendanceRecord = att != null,
                    // Legacy compatibility
                    IsPresent = att?.IsPresent ?? true,
                    DishPlanId = att?.DishPlanId,
                    AutoChargeExists = breakfastCharge + lunchCharge + dinnerCharge > 0,
                    AutoChargeAmount = breakfastCharge + lunchCharge + dinnerCharge
                });
            }
            return View(vm);
        }

        // User-specific view to see own attendance
        public async Task<IActionResult> MyAttendance()
        {
            var name = User.Identity?.Name;
            if (string.IsNullOrEmpty(name)) return Unauthorized();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == name);
            if (user == null) return NotFound();
            var attendances = await _context.Attendances
                .Where(a => a.UserId == user.UserId)
                .Include(a => a.BreakfastDishPlan)
                .Include(a => a.LunchDishPlan)
                .Include(a => a.DinnerDishPlan)
                .Include(a => a.MessPeriod)
                .ToListAsync();
            
            // Get all mess periods for grouping
            var messPeriods = await _context.MessPeriods
                .OrderByDescending(p => p.StartDate)
                .ToListAsync();
            ViewBag.MessPeriods = messPeriods;
            
            return View(attendances);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(MessManagement.Models.ViewModels.AttendanceMarkViewModel vm)
        {
            // Reload ViewBag data for potential return to view - filter by day of week
            var dayOfWeek = vm.Date.DayOfWeek.ToString();
            var dishPlansList = await _context.DishPlans.Where(d => d.DayOfWeek == dayOfWeek).ToListAsync();
            ViewBag.BreakfastDishes = dishPlansList.Where(d => d.MealType == "Breakfast")
                .Select(dp => new SelectListItem { Value = dp.DishPlanId.ToString(), Text = $"{dp.DishName} (Rs. {dp.Price})" }).ToList();
            ViewBag.LunchDishes = dishPlansList.Where(d => d.MealType == "Lunch")
                .Select(dp => new SelectListItem { Value = dp.DishPlanId.ToString(), Text = $"{dp.DishName} (Rs. {dp.Price})" }).ToList();
            ViewBag.DinnerDishes = dishPlansList.Where(d => d.MealType == "Dinner")
                .Select(dp => new SelectListItem { Value = dp.DishPlanId.ToString(), Text = $"{dp.DishName} (Rs. {dp.Price})" }).ToList();
            
            if (!ModelState.IsValid) return View("Index", vm);
            
            var date = vm.Date;
            
            foreach (var item in vm.Items)
            {
                var attendance = await _context.Attendances.FirstOrDefaultAsync(a => a.UserId == item.UserId && a.Date == date);
                
                if (attendance == null)
                {
                    attendance = new Attendance
                    {
                        UserId = item.UserId,
                        Date = date,
                        IsBreakfastPresent = item.IsBreakfastPresent,
                        IsLunchPresent = item.IsLunchPresent,
                        IsDinnerPresent = item.IsDinnerPresent,
                        BreakfastDishPlanId = item.IsBreakfastPresent ? item.BreakfastDishPlanId : null,
                        LunchDishPlanId = item.IsLunchPresent ? item.LunchDishPlanId : null,
                        DinnerDishPlanId = item.IsDinnerPresent ? item.DinnerDishPlanId : null,
                        // Set legacy IsPresent if any meal is present
                        IsPresent = item.IsBreakfastPresent || item.IsLunchPresent || item.IsDinnerPresent
                    };
                    _context.Attendances.Add(attendance);
                }
                else
                {
                    attendance.IsBreakfastPresent = item.IsBreakfastPresent;
                    attendance.IsLunchPresent = item.IsLunchPresent;
                    attendance.IsDinnerPresent = item.IsDinnerPresent;
                    attendance.BreakfastDishPlanId = item.IsBreakfastPresent ? item.BreakfastDishPlanId : null;
                    attendance.LunchDishPlanId = item.IsLunchPresent ? item.LunchDishPlanId : null;
                    attendance.DinnerDishPlanId = item.IsDinnerPresent ? item.DinnerDishPlanId : null;
                    attendance.IsPresent = item.IsBreakfastPresent || item.IsLunchPresent || item.IsDinnerPresent;
                    _context.Attendances.Update(attendance);
                }
                
                await _context.SaveChangesAsync();
                
                // Handle payments for each meal type
                await UpdateMealPayment(attendance, "Breakfast", item.IsBreakfastPresent, item.BreakfastDishPlanId, dishPlansList);
                await UpdateMealPayment(attendance, "Lunch", item.IsLunchPresent, item.LunchDishPlanId, dishPlansList);
                await UpdateMealPayment(attendance, "Dinner", item.IsDinnerPresent, item.DinnerDishPlanId, dishPlansList);
            }
            
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Attendance saved successfully!";
            return RedirectToAction(nameof(Index), new { date = vm.Date.ToString("yyyy-MM-dd") });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveBreakfast(MessManagement.Models.ViewModels.AttendanceMarkViewModel vm)
        {
            return await SaveMealType(vm, "Breakfast");
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveLunch(MessManagement.Models.ViewModels.AttendanceMarkViewModel vm)
        {
            return await SaveMealType(vm, "Lunch");
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveDinner(MessManagement.Models.ViewModels.AttendanceMarkViewModel vm)
        {
            return await SaveMealType(vm, "Dinner");
        }

        private async Task<IActionResult> SaveMealType(MessManagement.Models.ViewModels.AttendanceMarkViewModel vm, string mealType)
        {
            var dishPlansList = await _context.DishPlans.ToListAsync();
            var date = vm.Date;

            foreach (var item in vm.Items)
            {
                var attendance = await _context.Attendances.FirstOrDefaultAsync(a => a.UserId == item.UserId && a.Date == date);

                if (attendance == null)
                {
                    // When creating a new attendance record for a specific meal:
                    // - Only mark the specific meal as present/absent based on the form
                    // - Set other meals to false (not present) by default
                    // - All verifications start as false (pending)
                    attendance = new Attendance
                    {
                        UserId = item.UserId,
                        Date = date,
                        IsBreakfastPresent = mealType == "Breakfast" ? item.IsBreakfastPresent : false,
                        IsLunchPresent = mealType == "Lunch" ? item.IsLunchPresent : false,
                        IsDinnerPresent = mealType == "Dinner" ? item.IsDinnerPresent : false,
                        BreakfastDishPlanId = mealType == "Breakfast" && item.IsBreakfastPresent ? item.BreakfastDishPlanId : null,
                        LunchDishPlanId = mealType == "Lunch" && item.IsLunchPresent ? item.LunchDishPlanId : null,
                        DinnerDishPlanId = mealType == "Dinner" && item.IsDinnerPresent ? item.DinnerDishPlanId : null,
                        IsPresent = (mealType == "Breakfast" && item.IsBreakfastPresent) ||
                                    (mealType == "Lunch" && item.IsLunchPresent) ||
                                    (mealType == "Dinner" && item.IsDinnerPresent),
                        // All verifications start as false (pending) - will require verification
                        BreakfastVerified = false,
                        LunchVerified = false,
                        DinnerVerified = false
                    };
                    _context.Attendances.Add(attendance);
                }
                else
                {
                    // Update only the specific meal type
                    if (mealType == "Breakfast")
                    {
                        // Skip if already verified
                        if (!attendance.BreakfastVerified)
                        {
                            attendance.IsBreakfastPresent = item.IsBreakfastPresent;
                            attendance.BreakfastDishPlanId = item.IsBreakfastPresent ? item.BreakfastDishPlanId : null;
                        }
                    }
                    else if (mealType == "Lunch")
                    {
                        // Skip if already verified
                        if (!attendance.LunchVerified)
                        {
                            attendance.IsLunchPresent = item.IsLunchPresent;
                            attendance.LunchDishPlanId = item.IsLunchPresent ? item.LunchDishPlanId : null;
                        }
                    }
                    else if (mealType == "Dinner")
                    {
                        // Skip if already verified
                        if (!attendance.DinnerVerified)
                        {
                            attendance.IsDinnerPresent = item.IsDinnerPresent;
                            attendance.DinnerDishPlanId = item.IsDinnerPresent ? item.DinnerDishPlanId : null;
                        }
                    }
                    attendance.IsPresent = attendance.IsBreakfastPresent || attendance.IsLunchPresent || attendance.IsDinnerPresent;
                    _context.Attendances.Update(attendance);
                }

                await _context.SaveChangesAsync();

                // Handle payment for the specific meal type
                if (mealType == "Breakfast")
                    await UpdateMealPayment(attendance, "Breakfast", item.IsBreakfastPresent, item.BreakfastDishPlanId, dishPlansList);
                else if (mealType == "Lunch")
                    await UpdateMealPayment(attendance, "Lunch", item.IsLunchPresent, item.LunchDishPlanId, dishPlansList);
                else if (mealType == "Dinner")
                    await UpdateMealPayment(attendance, "Dinner", item.IsDinnerPresent, item.DinnerDishPlanId, dishPlansList);
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"{mealType} attendance saved successfully!";
            return RedirectToAction(nameof(Index), new { date = vm.Date.ToString("yyyy-MM-dd") });
        }

        private async Task UpdateMealPayment(Attendance attendance, string mealType, bool isPresent, int? dishPlanId, List<DishPlan> dishPlans)
        {
            // Find existing payment for this meal
            var existingPayment = await _context.Payments
                .FirstOrDefaultAsync(p => p.AttendanceId == attendance.AttendanceId 
                    && p.PaymentMethod == "Auto" 
                    && p.Remarks != null 
                    && p.Remarks.Contains(mealType));
            
            if (!isPresent || !dishPlanId.HasValue)
            {
                // Remove payment if not present or no dish selected
                if (existingPayment != null)
                {
                    _context.Payments.Remove(existingPayment);
                }
                return;
            }
            
            var dish = dishPlans.FirstOrDefault(d => d.DishPlanId == dishPlanId);
            if (dish == null || dish.Price <= 0) return;
            
            // Get the period
            var period = await _context.MessPeriods.FirstOrDefaultAsync(p => p.StartDate <= attendance.Date && p.EndDate >= attendance.Date)
                ?? await _context.MessPeriods.FirstOrDefaultAsync(p => p.IsActive);
            
            if (existingPayment != null)
            {
                // Update existing payment
                existingPayment.Amount = dish.Price;
                existingPayment.PeriodId = period?.PeriodId;
                existingPayment.Remarks = $"{mealType} - {dish.DishName} ({attendance.Date:yyyy-MM-dd})";
                _context.Payments.Update(existingPayment);
            }
            else
            {
                // Create new payment
                var payment = new Payment
                {
                    UserId = attendance.UserId,
                    AttendanceId = attendance.AttendanceId,
                    PeriodId = period?.PeriodId,
                    Amount = dish.Price,
                    PaymentDate = DateTime.Now,
                    PaymentMethod = "Auto",
                    Remarks = $"{mealType} - {dish.DishName} ({attendance.Date:yyyy-MM-dd})"
                };
                _context.Payments.Add(payment);
            }
        }

        // POST: Attendance/Verify - Legacy verify all at once
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify(int id)
        {
            var attendance = await _context.Attendances.FindAsync(id);
            if (attendance == null) return NotFound();
            
            // Verify all meals at once
            if (attendance.IsBreakfastPresent)
            {
                attendance.BreakfastVerified = true;
                attendance.BreakfastVerifiedOn = DateTime.Now;
            }
            if (attendance.IsLunchPresent)
            {
                attendance.LunchVerified = true;
                attendance.LunchVerifiedOn = DateTime.Now;
            }
            if (attendance.IsDinnerPresent)
            {
                attendance.DinnerVerified = true;
                attendance.DinnerVerifiedOn = DateTime.Now;
            }
            
            attendance.VerifiedByUser = true;
            attendance.VerifiedOn = DateTime.Now;
            await _context.SaveChangesAsync();
            
            TempData["SuccessMessage"] = $"All meals for {attendance.Date:MMM dd} verified successfully!";
            return RedirectToAction(nameof(MyAttendance));
        }

        // POST: Attendance/VerifyBreakfast - Verify breakfast only
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyBreakfast(int id)
        {
            var attendance = await _context.Attendances.FindAsync(id);
            if (attendance == null) return NotFound();
            
            attendance.BreakfastVerified = true;
            attendance.BreakfastVerifiedOn = DateTime.Now;
            
            // Update legacy field if all meals verified
            if (attendance.AllMealsVerified)
            {
                attendance.VerifiedByUser = true;
                attendance.VerifiedOn = DateTime.Now;
            }
            
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Breakfast for {attendance.Date:MMM dd} verified!";
            return RedirectToAction(nameof(MyAttendance));
        }

        // POST: Attendance/VerifyLunch - Verify lunch only
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyLunch(int id)
        {
            var attendance = await _context.Attendances.FindAsync(id);
            if (attendance == null) return NotFound();
            
            attendance.LunchVerified = true;
            attendance.LunchVerifiedOn = DateTime.Now;
            
            // Update legacy field if all meals verified
            if (attendance.AllMealsVerified)
            {
                attendance.VerifiedByUser = true;
                attendance.VerifiedOn = DateTime.Now;
            }
            
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Lunch for {attendance.Date:MMM dd} verified!";
            return RedirectToAction(nameof(MyAttendance));
        }

        // POST: Attendance/VerifyDinner - Verify dinner only
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyDinner(int id)
        {
            var attendance = await _context.Attendances.FindAsync(id);
            if (attendance == null) return NotFound();
            
            attendance.DinnerVerified = true;
            attendance.DinnerVerifiedOn = DateTime.Now;
            
            // Update legacy field if all meals verified
            if (attendance.AllMealsVerified)
            {
                attendance.VerifiedByUser = true;
                attendance.VerifiedOn = DateTime.Now;
            }
            
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Dinner for {attendance.Date:MMM dd} verified!";
            return RedirectToAction(nameof(MyAttendance));
        }

        // ============================================
        // MEMBER DECLINE ACTIONS: Member can decline attendance they were not present for
        // ============================================

        // POST: Attendance/DeclineBreakfast - Member declines breakfast (they weren't there)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeclineBreakfast(int id)
        {
            var attendance = await _context.Attendances.FindAsync(id);
            if (attendance == null) return NotFound();
            
            // Verify the attendance belongs to the current user
            var username = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null || attendance.UserId != user.UserId) return Unauthorized();
            
            // Mark as declined (not present) - admin will be notified
            attendance.IsBreakfastPresent = false;
            attendance.BreakfastDishPlanId = null;
            attendance.BreakfastVerified = false;
            attendance.BreakfastVerifiedOn = null;
            attendance.IsPresent = attendance.IsBreakfastPresent || attendance.IsLunchPresent || attendance.IsDinnerPresent;
            
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Breakfast for {attendance.Date:MMM dd} declined. Admin has been notified.";
            return RedirectToAction(nameof(MyAttendance));
        }

        // POST: Attendance/DeclineLunch - Member declines lunch
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeclineLunch(int id)
        {
            var attendance = await _context.Attendances.FindAsync(id);
            if (attendance == null) return NotFound();
            
            var username = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null || attendance.UserId != user.UserId) return Unauthorized();
            
            attendance.IsLunchPresent = false;
            attendance.LunchDishPlanId = null;
            attendance.LunchVerified = false;
            attendance.LunchVerifiedOn = null;
            attendance.IsPresent = attendance.IsBreakfastPresent || attendance.IsLunchPresent || attendance.IsDinnerPresent;
            
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Lunch for {attendance.Date:MMM dd} declined. Admin has been notified.";
            return RedirectToAction(nameof(MyAttendance));
        }

        // POST: Attendance/DeclineDinner - Member declines dinner
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeclineDinner(int id)
        {
            var attendance = await _context.Attendances.FindAsync(id);
            if (attendance == null) return NotFound();
            
            var username = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null || attendance.UserId != user.UserId) return Unauthorized();
            
            attendance.IsDinnerPresent = false;
            attendance.DinnerDishPlanId = null;
            attendance.DinnerVerified = false;
            attendance.DinnerVerifiedOn = null;
            attendance.IsPresent = attendance.IsBreakfastPresent || attendance.IsLunchPresent || attendance.IsDinnerPresent;
            
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Dinner for {attendance.Date:MMM dd} declined. Admin has been notified.";
            return RedirectToAction(nameof(MyAttendance));
        }

        // POST: Attendance/VerifyAllMeals - Verify all pending meals for all days
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyAllMeals()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return Unauthorized();
            
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return NotFound();
            
            var pendingAttendances = await _context.Attendances
                .Where(a => a.UserId == user.UserId)
                .ToListAsync();
            
            int verifiedCount = 0;
            foreach (var attendance in pendingAttendances)
            {
                bool changed = false;
                if (attendance.IsBreakfastPresent && !attendance.BreakfastVerified)
                {
                    attendance.BreakfastVerified = true;
                    attendance.BreakfastVerifiedOn = DateTime.Now;
                    changed = true;
                    verifiedCount++;
                }
                if (attendance.IsLunchPresent && !attendance.LunchVerified)
                {
                    attendance.LunchVerified = true;
                    attendance.LunchVerifiedOn = DateTime.Now;
                    changed = true;
                    verifiedCount++;
                }
                if (attendance.IsDinnerPresent && !attendance.DinnerVerified)
                {
                    attendance.DinnerVerified = true;
                    attendance.DinnerVerifiedOn = DateTime.Now;
                    changed = true;
                    verifiedCount++;
                }
                
                if (changed && attendance.AllMealsVerified)
                {
                    attendance.VerifiedByUser = true;
                    attendance.VerifiedOn = DateTime.Now;
                }
            }
            
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"{verifiedCount} meals verified successfully!";
            return RedirectToAction(nameof(MyAttendance));
        }

        // ============================================
        // ADMIN ACTIONS: Resend verification or cancel attendance
        // ============================================

        // POST: Attendance/AdminResendVerification - Admin can resend verification request for a specific meal
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminResendVerification(int id, string mealType)
        {
            var attendance = await _context.Attendances.FindAsync(id);
            if (attendance == null) return NotFound();
            
            // Reset the verification status for the specific meal type so member can verify again
            switch (mealType.ToLower())
            {
                case "breakfast":
                    attendance.BreakfastVerified = false;
                    attendance.BreakfastVerifiedOn = null;
                    break;
                case "lunch":
                    attendance.LunchVerified = false;
                    attendance.LunchVerifiedOn = null;
                    break;
                case "dinner":
                    attendance.DinnerVerified = false;
                    attendance.DinnerVerifiedOn = null;
                    break;
                default:
                    return BadRequest("Invalid meal type");
            }
            
            attendance.VerifiedByUser = false;
            attendance.VerifiedOn = null;
            
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"{mealType} verification request re-sent for {attendance.Date:MMM dd}!";
            return RedirectToAction(nameof(Index), new { date = attendance.Date.ToString("yyyy-MM-dd") });
        }

        // POST: Attendance/AdminCancelMeal - Admin can cancel a specific meal attendance that member declined
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminCancelMeal(int id, string mealType)
        {
            var attendance = await _context.Attendances.FindAsync(id);
            if (attendance == null) return NotFound();
            
            // Remove the meal attendance entirely
            switch (mealType.ToLower())
            {
                case "breakfast":
                    attendance.IsBreakfastPresent = false;
                    attendance.BreakfastDishPlanId = null;
                    attendance.BreakfastVerified = false;
                    attendance.BreakfastVerifiedOn = null;
                    break;
                case "lunch":
                    attendance.IsLunchPresent = false;
                    attendance.LunchDishPlanId = null;
                    attendance.LunchVerified = false;
                    attendance.LunchVerifiedOn = null;
                    break;
                case "dinner":
                    attendance.IsDinnerPresent = false;
                    attendance.DinnerDishPlanId = null;
                    attendance.DinnerVerified = false;
                    attendance.DinnerVerifiedOn = null;
                    break;
                default:
                    return BadRequest("Invalid meal type");
            }
            
            // Update legacy IsPresent field
            attendance.IsPresent = attendance.IsBreakfastPresent || attendance.IsLunchPresent || attendance.IsDinnerPresent;
            
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"{mealType} cancelled for {attendance.Date:MMM dd}!";
            return RedirectToAction(nameof(Index), new { date = attendance.Date.ToString("yyyy-MM-dd") });
        }

        // Legacy methods for backward compatibility
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Mark(int userId, DateTime date)
        {
            var attendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.UserId == userId && a.Date == date);
            if (attendance == null)
            {
                attendance = new Attendance { UserId = userId, Date = date, IsPresent = false, IsLunchPresent = false, IsDinnerPresent = false };
                _context.Attendances.Add(attendance);
            }
            else
            {
                attendance.IsPresent = !attendance.IsPresent;
                _context.Attendances.Update(attendance);
            }
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
