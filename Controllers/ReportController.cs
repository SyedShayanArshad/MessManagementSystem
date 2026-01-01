using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MessManagement.Data;
using MessManagement.Models;
using System.Text;

namespace MessManagement.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ReportController : Controller
    {
        private readonly ApplicationDbContext _context;
        public ReportController(ApplicationDbContext context) => _context = context;

        // Helper method to generate report data
        private async Task<(List<dynamic> Report, MessPeriod Period, int TeaTotalCups, decimal TotalPayments)> GenerateReportData(int periodId)
        {
            var period = await _context.MessPeriods.FindAsync(periodId);
            if (period == null) return (new List<dynamic>(), null!, 0, 0);

            var start = period.StartDate;
            var end = period.EndDate;

            var users = await _context.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
            var report = new List<dynamic>();

            var teaTotalCups = await _context.TeaRecords
                .Where(t => t.PeriodId == periodId || (t.Date >= start && t.Date <= end))
                .SumAsync(t => (int?)t.TotalCupsServed) ?? 0;
            
            var allTeaEntries = await _context.TeaEntries
                .Where(t => t.PeriodId == periodId || (t.Date >= start && t.Date <= end))
                .ToListAsync();
            
            var totalPayments = await _context.Payments
                .Where(p => p.PeriodId == periodId)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            foreach (var user in users)
            {
                var attendances = await _context.Attendances
                    .Where(a => a.UserId == user.UserId && a.Date >= start && a.Date <= end)
                    .Include(a => a.BreakfastDishPlan)
                    .Include(a => a.LunchDishPlan)
                    .Include(a => a.DinnerDishPlan)
                    .ToListAsync();
                
                decimal mealCharges = 0;
                int breakfastCount = 0, lunchCount = 0, dinnerCount = 0;
                
                foreach (var att in attendances)
                {
                    if (att.IsBreakfastPresent && att.BreakfastDishPlan != null)
                    {
                        mealCharges += att.BreakfastDishPlan.Price;
                        breakfastCount++;
                    }
                    if (att.IsLunchPresent && att.LunchDishPlan != null)
                    {
                        mealCharges += att.LunchDishPlan.Price;
                        lunchCount++;
                    }
                    if (att.IsDinnerPresent && att.DinnerDishPlan != null)
                    {
                        mealCharges += att.DinnerDishPlan.Price;
                        dinnerCount++;
                    }
                }
                
                decimal waterCharges = period.FixedWaterCharge;
                var userTeaEntries = allTeaEntries.Where(t => t.UserId == user.UserId).ToList();
                int userTeaCups = userTeaEntries.Sum(t => t.Cups);
                decimal teaCharges = userTeaCups * period.TeaPricePerCup;
                decimal totalCharges = mealCharges + waterCharges + teaCharges;
                
                var payments = await _context.Payments
                    .Where(p => p.UserId == user.UserId && p.PeriodId == periodId)
                    .SumAsync(p => (decimal?)p.Amount) ?? 0m;
                    
                report.Add(new
                {
                    User = user,
                    AttendanceCount = attendances.Count(a => a.IsBreakfastPresent || a.IsLunchPresent || a.IsDinnerPresent),
                    BreakfastCount = breakfastCount,
                    LunchCount = lunchCount,
                    DinnerCount = dinnerCount,
                    MealCharges = mealCharges,
                    WaterCharges = waterCharges,
                    TeaCups = userTeaCups,
                    TeaCharges = teaCharges,
                    TotalCharges = totalCharges,
                    Payments = payments,
                    Balance = totalCharges - payments
                });
            }

            return (report, period, teaTotalCups, totalPayments);
        }

        public async Task<IActionResult> Index(int? periodId)
        {
            var periods = await _context.MessPeriods.OrderByDescending(p => p.StartDate).ToListAsync();
            ViewBag.Periods = periods;

            if (periodId == null)
            {
                var activePeriod = periods.FirstOrDefault(p => p.IsActive);
                periodId = activePeriod?.PeriodId ?? periods.FirstOrDefault()?.PeriodId;
            }
            
            if (periodId == null) return View();

            var (report, period, teaTotalCups, totalPayments) = await GenerateReportData(periodId.Value);
            if (period == null) return View();

            ViewBag.Report = report;
            ViewBag.TeaTotalCups = teaTotalCups;
            ViewBag.TotalPayments = totalPayments;
            ViewBag.SelectedPeriod = period;

            return View();
        }

        // Export Overall Summary CSV
        [HttpGet]
        public async Task<IActionResult> ExportSummary(int periodId)
        {
            var (report, period, teaTotalCups, totalPayments) = await GenerateReportData(periodId);
            if (period == null) return NotFound();

            var sb = new StringBuilder();
            sb.AppendLine("Mess Management - Period Summary Report");
            sb.AppendLine($"Period: {period.PeriodName}");
            sb.AppendLine($"Date Range: {period.StartDate:MMM dd, yyyy} - {period.EndDate:MMM dd, yyyy}");
            sb.AppendLine($"Generated: {DateTime.Now:MMM dd, yyyy HH:mm}");
            sb.AppendLine();
            
            // Summary Stats
            var totalCharges = report.Sum(x => (decimal)x.TotalCharges);
            var totalMealCharges = report.Sum(x => (decimal)x.MealCharges);
            var totalWaterCharges = report.Sum(x => (decimal)x.WaterCharges);
            var totalTeaCharges = report.Sum(x => (decimal)x.TeaCharges);
            var totalBalance = report.Sum(x => (decimal)x.Balance);
            var membersWithDues = report.Count(x => (decimal)x.Balance > 0);
            
            sb.AppendLine("=== SUMMARY ===");
            sb.AppendLine($"Total Members,{report.Count}");
            sb.AppendLine($"Total Meal Charges,Rs. {totalMealCharges:N0}");
            sb.AppendLine($"Total Water Charges,Rs. {totalWaterCharges:N0}");
            sb.AppendLine($"Total Tea Charges,Rs. {totalTeaCharges:N0}");
            sb.AppendLine($"Total Tea Cups,{teaTotalCups}");
            sb.AppendLine($"Grand Total Charges,Rs. {totalCharges:N0}");
            sb.AppendLine($"Total Collected,Rs. {totalPayments:N0}");
            sb.AppendLine($"Outstanding Balance,Rs. {totalBalance:N0}");
            sb.AppendLine($"Collection Rate,{(totalCharges > 0 ? (totalPayments / totalCharges * 100) : 0):N1}%");
            sb.AppendLine($"Members with Dues,{membersWithDues}");
            sb.AppendLine($"Fully Paid Members,{report.Count - membersWithDues}");
            sb.AppendLine();
            
            // Member-wise summary
            sb.AppendLine("=== MEMBER-WISE SUMMARY ===");
            sb.AppendLine("Name,Breakfast,Lunch,Dinner,Total Meals,Meal Charges,Tea Cups,Tea Charges,Water Charges,Total Charges,Paid,Balance,Status");
            
            foreach (dynamic item in report)
            {
                var balance = (decimal)item.Balance;
                var status = balance > 0 ? "Due" : "Paid";
                sb.AppendLine($"\"{item.User.FullName}\",{item.BreakfastCount},{item.LunchCount},{item.DinnerCount},{item.AttendanceCount},Rs. {item.MealCharges:N0},{item.TeaCups},Rs. {item.TeaCharges:N0},Rs. {item.WaterCharges:N0},Rs. {item.TotalCharges:N0},Rs. {item.Payments:N0},Rs. {balance:N0},{status}");
            }
            
            // Footer totals
            sb.AppendLine();
            var totalBreakfast = report.Sum(x => (int)x.BreakfastCount);
            var totalLunch = report.Sum(x => (int)x.LunchCount);
            var totalDinner = report.Sum(x => (int)x.DinnerCount);
            var totalMeals = report.Sum(x => (int)x.AttendanceCount);
            var totalTeaCups = report.Sum(x => (int)x.TeaCups);
            
            sb.AppendLine($"TOTALS,{totalBreakfast},{totalLunch},{totalDinner},{totalMeals},Rs. {totalMealCharges:N0},{totalTeaCups},Rs. {totalTeaCharges:N0},Rs. {totalWaterCharges:N0},Rs. {totalCharges:N0},Rs. {totalPayments:N0},Rs. {totalBalance:N0},");

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"MessReport_Summary_{period.PeriodName.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.csv";
            Response.Headers.Append("Content-Disposition", $"attachment; filename={fileName}");
            return File(bytes, "text/csv", fileName);
        }

        // Export User-wise Detailed CSV
        [HttpGet]
        public async Task<IActionResult> ExportUserDetail(int periodId, int? userId = null)
        {
            var period = await _context.MessPeriods.FindAsync(periodId);
            if (period == null) return NotFound();

            var start = period.StartDate;
            var end = period.EndDate;
            
            var usersQuery = _context.Users.Where(u => u.IsActive);
            if (userId.HasValue)
                usersQuery = usersQuery.Where(u => u.UserId == userId.Value);
            
            var users = await usersQuery.OrderBy(u => u.FullName).ToListAsync();
            
            var allTeaEntries = await _context.TeaEntries
                .Where(t => t.PeriodId == periodId || (t.Date >= start && t.Date <= end))
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("Mess Management - Detailed User Report");
            sb.AppendLine($"Period: {period.PeriodName}");
            sb.AppendLine($"Date Range: {period.StartDate:MMM dd, yyyy} - {period.EndDate:MMM dd, yyyy}");
            sb.AppendLine($"Generated: {DateTime.Now:MMM dd, yyyy HH:mm}");
            sb.AppendLine();

            foreach (var user in users)
            {
                var attendances = await _context.Attendances
                    .Where(a => a.UserId == user.UserId && a.Date >= start && a.Date <= end)
                    .Include(a => a.BreakfastDishPlan)
                    .Include(a => a.LunchDishPlan)
                    .Include(a => a.DinnerDishPlan)
                    .OrderBy(a => a.Date)
                    .ToListAsync();

                var payments = await _context.Payments
                    .Where(p => p.UserId == user.UserId && p.PeriodId == periodId)
                    .OrderBy(p => p.PaymentDate)
                    .ToListAsync();

                var userTeaEntries = allTeaEntries.Where(t => t.UserId == user.UserId).ToList();
                
                sb.AppendLine($"=== {user.FullName} ===");
                sb.AppendLine($"Username: {user.Username}");
                sb.AppendLine();
                
                // Daily Attendance
                sb.AppendLine("--- Daily Meal Attendance ---");
                sb.AppendLine("Date,Day,Breakfast,Breakfast Dish,Breakfast Charge,Lunch,Lunch Dish,Lunch Charge,Dinner,Dinner Dish,Dinner Charge,Day Total");
                
                decimal totalMealCharges = 0;
                int bCount = 0, lCount = 0, dCount = 0;
                
                foreach (var att in attendances)
                {
                    var bPresent = att.IsBreakfastPresent ? "Yes" : "No";
                    var bDish = att.BreakfastDishPlan?.DishName ?? "-";
                    var bCharge = att.IsBreakfastPresent && att.BreakfastDishPlan != null ? att.BreakfastDishPlan.Price : 0;
                    
                    var lPresent = att.IsLunchPresent ? "Yes" : "No";
                    var lDish = att.LunchDishPlan?.DishName ?? "-";
                    var lCharge = att.IsLunchPresent && att.LunchDishPlan != null ? att.LunchDishPlan.Price : 0;
                    
                    var dPresent = att.IsDinnerPresent ? "Yes" : "No";
                    var dDish = att.DinnerDishPlan?.DishName ?? "-";
                    var dCharge = att.IsDinnerPresent && att.DinnerDishPlan != null ? att.DinnerDishPlan.Price : 0;
                    
                    var dayTotal = bCharge + lCharge + dCharge;
                    totalMealCharges += dayTotal;
                    
                    if (att.IsBreakfastPresent) bCount++;
                    if (att.IsLunchPresent) lCount++;
                    if (att.IsDinnerPresent) dCount++;
                    
                    sb.AppendLine($"{att.Date:yyyy-MM-dd},{att.Date:ddd},{bPresent},\"{bDish}\",Rs. {bCharge:N0},{lPresent},\"{lDish}\",Rs. {lCharge:N0},{dPresent},\"{dDish}\",Rs. {dCharge:N0},Rs. {dayTotal:N0}");
                }
                
                sb.AppendLine($"MEAL TOTALS,, {bCount} days,,Rs. {attendances.Where(a => a.IsBreakfastPresent && a.BreakfastDishPlan != null).Sum(a => a.BreakfastDishPlan!.Price):N0},{lCount} days,,Rs. {attendances.Where(a => a.IsLunchPresent && a.LunchDishPlan != null).Sum(a => a.LunchDishPlan!.Price):N0},{dCount} days,,Rs. {attendances.Where(a => a.IsDinnerPresent && a.DinnerDishPlan != null).Sum(a => a.DinnerDishPlan!.Price):N0},Rs. {totalMealCharges:N0}");
                sb.AppendLine();
                
                // Tea Entries
                sb.AppendLine("--- Tea Consumption ---");
                sb.AppendLine("Date,Cups,Charge");
                decimal totalTeaCharges = 0;
                foreach (var tea in userTeaEntries.OrderBy(t => t.Date))
                {
                    var teaCharge = tea.Cups * period.TeaPricePerCup;
                    totalTeaCharges += teaCharge;
                    sb.AppendLine($"{tea.Date:yyyy-MM-dd},{tea.Cups},Rs. {teaCharge:N0}");
                }
                sb.AppendLine($"TEA TOTAL,{userTeaEntries.Sum(t => t.Cups)},Rs. {totalTeaCharges:N0}");
                sb.AppendLine();
                
                // Payments
                sb.AppendLine("--- Payment History ---");
                sb.AppendLine("Date,Amount,Method,Reference,Status");
                foreach (var pay in payments)
                {
                    sb.AppendLine($"{pay.PaymentDate:yyyy-MM-dd},Rs. {pay.Amount:N0},{pay.PaymentMethod},{pay.ReferenceNumber ?? "-"},{pay.Status}");
                }
                sb.AppendLine($"PAYMENT TOTAL,,Rs. {payments.Sum(p => p.Amount):N0},,");
                sb.AppendLine();
                
                // Summary
                var grandTotal = totalMealCharges + period.FixedWaterCharge + totalTeaCharges;
                var totalPaid = payments.Sum(p => p.Amount);
                var balance = grandTotal - totalPaid;
                
                sb.AppendLine("--- Bill Summary ---");
                sb.AppendLine($"Meal Charges,Rs. {totalMealCharges:N0}");
                sb.AppendLine($"Water Charges,Rs. {period.FixedWaterCharge:N0}");
                sb.AppendLine($"Tea Charges,Rs. {totalTeaCharges:N0}");
                sb.AppendLine($"Grand Total,Rs. {grandTotal:N0}");
                sb.AppendLine($"Amount Paid,Rs. {totalPaid:N0}");
                sb.AppendLine($"Balance Due,Rs. {balance:N0}");
                sb.AppendLine();
                sb.AppendLine("========================================");
                sb.AppendLine();
            }

            var fileName = userId.HasValue 
                ? $"MessReport_User_{users.FirstOrDefault()?.FullName?.Replace(" ", "_")}_{period.PeriodName.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.csv"
                : $"MessReport_AllUsers_{period.PeriodName.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.csv";

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            Response.Headers.Append("Content-Disposition", $"attachment; filename={fileName}");
            return File(bytes, "text/csv", fileName);
        }
    }
}
