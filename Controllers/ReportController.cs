using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MessManagement.Data;
using MessManagement.Models;
using MessManagement.Services;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MessManagement.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ReportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<ReportController> _logger;
        
        static ReportController()
        {
            // Set QuestPDF license (Community license is free for revenue < $1M)
            QuestPDF.Settings.License = LicenseType.Community;
        }
        
        public ReportController(ApplicationDbContext context, IEmailService emailService, ILogger<ReportController> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

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
public async Task ExportSummaryCsv(int periodId)
{
    var (report, period, teaTotalCups, totalPayments) = await GenerateReportData(periodId);
    if (period == null) return;

    Response.Clear();
    Response.ContentType = "text/csv; charset=utf-8";
    Response.Headers.Add(
        "Content-Disposition",
        $"attachment; filename=\"MessReport_Summary_{period.PeriodName.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.csv\""
    );

    await using var writer = new StreamWriter(Response.Body, Encoding.UTF8);

    writer.WriteLine("Mess Management - Period Summary Report");
    writer.WriteLine($"Period: {period.PeriodName}");
    writer.WriteLine($"Generated: {DateTime.Now:MMM dd, yyyy HH:mm}");
    writer.WriteLine();

    writer.WriteLine("Name,Total Charges,Paid,Balance");

    foreach (dynamic item in report)
    {
        writer.WriteLine(
            $"\"{item.User.FullName}\",{item.TotalCharges},{item.Payments},{item.Balance}");
    }

    await writer.FlushAsync();
}


        // Export Summary to PDF using QuestPDF
        [HttpGet]
        public async Task<IActionResult> ExportSummaryPdf(int periodId)
        {
            var (report, period, teaTotalCups, totalPayments) = await GenerateReportData(periodId);
            if (period == null) return NotFound();

            var totalCharges = report.Sum(x => (decimal)x.TotalCharges);
            var totalMealCharges = report.Sum(x => (decimal)x.MealCharges);
            var totalWaterCharges = report.Sum(x => (decimal)x.WaterCharges);
            var totalTeaCharges = report.Sum(x => (decimal)x.TeaCharges);
            var totalBalance = report.Sum(x => (decimal)x.Balance);
            var membersWithDues = report.Count(x => (decimal)x.Balance > 0);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Column(col =>
                    {
                        col.Item().Text("Mess Management - Period Summary Report")
                            .FontSize(18).Bold().FontColor(Colors.Blue.Darken2);
                        col.Item().Text($"Period: {period.PeriodName}").FontSize(12);
                        col.Item().Text($"Date Range: {period.StartDate:MMM dd, yyyy} - {period.EndDate:MMM dd, yyyy}");
                        col.Item().Text($"Generated: {DateTime.Now:MMM dd, yyyy HH:mm}");
                        col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                    });

                    page.Content().Column(col =>
                    {
                        // Summary Section
                        col.Item().PaddingBottom(10).Row(row =>
                        {
                            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Column(c =>
                            {
                                c.Item().Text("Summary Statistics").Bold().FontSize(12);
                                c.Item().Text($"Total Members: {report.Count}");
                                c.Item().Text($"Meal Charges: Rs. {totalMealCharges:N0}");
                                c.Item().Text($"Water Charges: Rs. {totalWaterCharges:N0}");
                                c.Item().Text($"Tea Charges: Rs. {totalTeaCharges:N0}");
                            });
                            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Column(c =>
                            {
                                c.Item().Text("Financial Summary").Bold().FontSize(12);
                                c.Item().Text($"Grand Total: Rs. {totalCharges:N0}");
                                c.Item().Text($"Total Collected: Rs. {totalPayments:N0}");
                                c.Item().Text($"Outstanding: Rs. {totalBalance:N0}");
                                c.Item().Text($"Members with Dues: {membersWithDues}");
                            });
                        });

                        // Member Table
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3); // Name
                                columns.RelativeColumn(1); // Breakfast
                                columns.RelativeColumn(1); // Lunch
                                columns.RelativeColumn(1); // Dinner
                                columns.RelativeColumn(1.5f); // Meal Charges
                                columns.RelativeColumn(1); // Tea
                                columns.RelativeColumn(1.5f); // Tea Charges
                                columns.RelativeColumn(1.5f); // Water
                                columns.RelativeColumn(1.5f); // Total
                                columns.RelativeColumn(1.5f); // Paid
                                columns.RelativeColumn(1.5f); // Balance
                                columns.RelativeColumn(1); // Status
                            });

                            // Header
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Blue.Lighten3).Padding(5).Text("Name").Bold();
                                header.Cell().Background(Colors.Blue.Lighten3).Padding(5).AlignCenter().Text("B").Bold();
                                header.Cell().Background(Colors.Blue.Lighten3).Padding(5).AlignCenter().Text("L").Bold();
                                header.Cell().Background(Colors.Blue.Lighten3).Padding(5).AlignCenter().Text("D").Bold();
                                header.Cell().Background(Colors.Blue.Lighten3).Padding(5).AlignRight().Text("Meals").Bold();
                                header.Cell().Background(Colors.Blue.Lighten3).Padding(5).AlignCenter().Text("Tea").Bold();
                                header.Cell().Background(Colors.Blue.Lighten3).Padding(5).AlignRight().Text("Tea Rs.").Bold();
                                header.Cell().Background(Colors.Blue.Lighten3).Padding(5).AlignRight().Text("Water").Bold();
                                header.Cell().Background(Colors.Blue.Lighten3).Padding(5).AlignRight().Text("Total").Bold();
                                header.Cell().Background(Colors.Blue.Lighten3).Padding(5).AlignRight().Text("Paid").Bold();
                                header.Cell().Background(Colors.Blue.Lighten3).Padding(5).AlignRight().Text("Balance").Bold();
                                header.Cell().Background(Colors.Blue.Lighten3).Padding(5).AlignCenter().Text("Status").Bold();
                            });

                            // Data rows
                            foreach (dynamic item in report)
                            {
                                var balance = (decimal)item.Balance;
                                var bgColor = balance > 0 ? Colors.Red.Lighten5 : Colors.Green.Lighten5;

                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text((string)item.User.FullName);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignCenter().Text(((int)item.BreakfastCount).ToString());
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignCenter().Text(((int)item.LunchCount).ToString());
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignCenter().Text(((int)item.DinnerCount).ToString());
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight().Text($"Rs. {(decimal)item.MealCharges:N0}");
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignCenter().Text(((int)item.TeaCups).ToString());
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight().Text($"Rs. {(decimal)item.TeaCharges:N0}");
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight().Text($"Rs. {(decimal)item.WaterCharges:N0}");
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight().Text($"Rs. {(decimal)item.TotalCharges:N0}");
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight().Text($"Rs. {(decimal)item.Payments:N0}").FontColor(Colors.Green.Darken2);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Background(bgColor).Padding(4).AlignRight().Text($"Rs. {balance:N0}").FontColor(balance > 0 ? Colors.Red.Darken2 : Colors.Green.Darken2);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Background(bgColor).Padding(4).AlignCenter().Text(balance > 0 ? "Due" : "Paid").FontColor(balance > 0 ? Colors.Red.Darken2 : Colors.Green.Darken2);
                            }

                            // Footer totals
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("TOTALS").Bold();
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignCenter().Text(report.Sum(x => (int)x.BreakfastCount).ToString()).Bold();
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignCenter().Text(report.Sum(x => (int)x.LunchCount).ToString()).Bold();
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignCenter().Text(report.Sum(x => (int)x.DinnerCount).ToString()).Bold();
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text($"Rs. {totalMealCharges:N0}").Bold();
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignCenter().Text(report.Sum(x => (int)x.TeaCups).ToString()).Bold();
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text($"Rs. {totalTeaCharges:N0}").Bold();
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text($"Rs. {totalWaterCharges:N0}").Bold();
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text($"Rs. {totalCharges:N0}").Bold();
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text($"Rs. {totalPayments:N0}").Bold();
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text($"Rs. {totalBalance:N0}").Bold();
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("");
                        });
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" of ");
                        x.TotalPages();
                    });
                });
            });

            var pdfBytes = document.GeneratePdf();
            var fileName = $"MessReport_Summary_{period.PeriodName.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.pdf";
            
            return File(pdfBytes, "application/pdf", fileName);
        }

        // Export User-wise Detailed CSV
        [HttpGet]
        public async Task<IActionResult> ExportUserDetailCsv(int periodId, int? userId = null)
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
            
            return File(bytes, "text/csv", fileName);
        }

        // Export User Detail to PDF using QuestPDF
        [HttpGet]
        public async Task<IActionResult> ExportUserDetailPdf(int periodId, int? userId = null)
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

            var document = Document.Create(container =>
            {
                foreach (var user in users)
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(30);
                        page.DefaultTextStyle(x => x.FontSize(9));

                        var attendances = _context.Attendances
                            .Where(a => a.UserId == user.UserId && a.Date >= start && a.Date <= end)
                            .Include(a => a.BreakfastDishPlan)
                            .Include(a => a.LunchDishPlan)
                            .Include(a => a.DinnerDishPlan)
                            .OrderBy(a => a.Date)
                            .ToList();

                        var payments = _context.Payments
                            .Where(p => p.UserId == user.UserId && p.PeriodId == periodId)
                            .OrderBy(p => p.PaymentDate)
                            .ToList();

                        var userTeaEntries = allTeaEntries.Where(t => t.UserId == user.UserId).ToList();

                        decimal totalMealCharges = 0;
                        int bCount = 0, lCount = 0, dCount = 0;
                        foreach (var att in attendances)
                        {
                            if (att.IsBreakfastPresent && att.BreakfastDishPlan != null) { totalMealCharges += att.BreakfastDishPlan.Price; bCount++; }
                            if (att.IsLunchPresent && att.LunchDishPlan != null) { totalMealCharges += att.LunchDishPlan.Price; lCount++; }
                            if (att.IsDinnerPresent && att.DinnerDishPlan != null) { totalMealCharges += att.DinnerDishPlan.Price; dCount++; }
                        }
                        decimal totalTeaCharges = userTeaEntries.Sum(t => t.Cups * period.TeaPricePerCup);
                        decimal totalPaid = payments.Sum(p => p.Amount);
                        decimal grandTotal = totalMealCharges + period.FixedWaterCharge + totalTeaCharges;
                        decimal balance = grandTotal - totalPaid;

                        page.Header().Column(col =>
                        {
                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("Mess Management").FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
                                    c.Item().Text("Member Detailed Report").FontSize(11);
                                });
                                row.RelativeItem().AlignRight().Column(c =>
                                {
                                    c.Item().Text(user.FullName).FontSize(12).Bold();
                                    c.Item().Text($"@{user.Username}").FontSize(9).FontColor(Colors.Grey.Darken1);
                                });
                            });
                            col.Item().PaddingTop(5).Text($"Period: {period.PeriodName} ({period.StartDate:MMM dd} - {period.EndDate:MMM dd, yyyy})").FontSize(9);
                            col.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                        });

                        page.Content().Column(col =>
                        {
                            // Bill Summary Box at top
                            col.Item().PaddingBottom(10).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Row(row =>
                            {
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("Bill Summary").Bold().FontSize(11);
                                    c.Item().PaddingTop(5).Text($"Meals: {bCount}B + {lCount}L + {dCount}D = Rs. {totalMealCharges:N0}");
                                    c.Item().Text($"Water: Rs. {period.FixedWaterCharge:N0}");
                                    c.Item().Text($"Tea ({userTeaEntries.Sum(t => t.Cups)} cups): Rs. {totalTeaCharges:N0}");
                                });
                                row.RelativeItem().AlignRight().Column(c =>
                                {
                                    c.Item().Text($"Grand Total: Rs. {grandTotal:N0}").Bold();
                                    c.Item().Text($"Paid: Rs. {totalPaid:N0}").FontColor(Colors.Green.Darken2);
                                    c.Item().Text($"Balance: Rs. {balance:N0}").Bold().FontColor(balance > 0 ? Colors.Red.Darken2 : Colors.Green.Darken2);
                                });
                            });

                            // Attendance Table
                            if (attendances.Any())
                            {
                                col.Item().PaddingBottom(5).Text("Daily Meal Attendance").Bold().FontSize(10);
                                col.Item().PaddingBottom(10).Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2); // Date
                                        columns.RelativeColumn(1); // Day
                                        columns.RelativeColumn(1); // B
                                        columns.RelativeColumn(1); // L
                                        columns.RelativeColumn(1); // D
                                        columns.RelativeColumn(2); // Total
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().Background(Colors.Blue.Lighten4).Padding(3).Text("Date").Bold();
                                        header.Cell().Background(Colors.Blue.Lighten4).Padding(3).Text("Day").Bold();
                                        header.Cell().Background(Colors.Blue.Lighten4).Padding(3).AlignCenter().Text("B").Bold();
                                        header.Cell().Background(Colors.Blue.Lighten4).Padding(3).AlignCenter().Text("L").Bold();
                                        header.Cell().Background(Colors.Blue.Lighten4).Padding(3).AlignCenter().Text("D").Bold();
                                        header.Cell().Background(Colors.Blue.Lighten4).Padding(3).AlignRight().Text("Total").Bold();
                                    });

                                    foreach (var att in attendances)
                                    {
                                        var bCharge = att.IsBreakfastPresent && att.BreakfastDishPlan != null ? att.BreakfastDishPlan.Price : 0;
                                        var lCharge = att.IsLunchPresent && att.LunchDishPlan != null ? att.LunchDishPlan.Price : 0;
                                        var dCharge = att.IsDinnerPresent && att.DinnerDishPlan != null ? att.DinnerDishPlan.Price : 0;
                                        var dayTotal = bCharge + lCharge + dCharge;

                                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(att.Date.ToString("MMM dd"));
                                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(att.Date.ToString("ddd"));
                                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(2).AlignCenter().Text(att.IsBreakfastPresent ? "✓" : "-").FontColor(att.IsBreakfastPresent ? Colors.Green.Darken2 : Colors.Grey.Lighten1);
                                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(2).AlignCenter().Text(att.IsLunchPresent ? "✓" : "-").FontColor(att.IsLunchPresent ? Colors.Green.Darken2 : Colors.Grey.Lighten1);
                                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(2).AlignCenter().Text(att.IsDinnerPresent ? "✓" : "-").FontColor(att.IsDinnerPresent ? Colors.Green.Darken2 : Colors.Grey.Lighten1);
                                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(2).AlignRight().Text($"Rs. {dayTotal:N0}");
                                    }
                                });
                            }

                            // Payments Table
                            if (payments.Any())
                            {
                                col.Item().PaddingBottom(5).Text("Payment History").Bold().FontSize(10);
                                col.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(2);
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().Background(Colors.Green.Lighten4).Padding(3).Text("Date").Bold();
                                        header.Cell().Background(Colors.Green.Lighten4).Padding(3).AlignRight().Text("Amount").Bold();
                                        header.Cell().Background(Colors.Green.Lighten4).Padding(3).Text("Method").Bold();
                                        header.Cell().Background(Colors.Green.Lighten4).Padding(3).Text("Reference").Bold();
                                    });

                                    foreach (var pay in payments)
                                    {
                                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(pay.PaymentDate.ToString("MMM dd, yyyy"));
                                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(2).AlignRight().Text($"Rs. {pay.Amount:N0}");
                                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(pay.PaymentMethod);
                                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(pay.ReferenceNumber ?? "-");
                                    }
                                });
                            }
                        });

                        page.Footer().AlignCenter().Text(x =>
                        {
                            x.Span($"Generated on {DateTime.Now:MMM dd, yyyy HH:mm} | Page ");
                            x.CurrentPageNumber();
                            x.Span(" of ");
                            x.TotalPages();
                        });
                    });
                }
            });

            var pdfBytes = document.GeneratePdf();
            var fileName = userId.HasValue 
                ? $"MessReport_User_{users.FirstOrDefault()?.FullName?.Replace(" ", "_")}_{period.PeriodName.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.pdf"
                : $"MessReport_AllUsers_{period.PeriodName.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.pdf";
            
            return File(pdfBytes, "application/pdf", fileName);
        }

        // Send bill statement emails to all users for a period
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendBillStatementEmails(int periodId)
        {
            var period = await _context.MessPeriods.FindAsync(periodId);
            if (period == null)
            {
                TempData["ErrorMessage"] = "Period not found.";
                return RedirectToAction(nameof(Index));
            }

            var start = period.StartDate;
            var end = period.EndDate;

            // Get all active users with email
            var users = await _context.Users
                .Where(u => u.IsActive && !string.IsNullOrEmpty(u.Email))
                .OrderBy(u => u.FullName)
                .ToListAsync();

            if (!users.Any())
            {
                TempData["ErrorMessage"] = "No users with email addresses found.";
                return RedirectToAction(nameof(Index));
            }

            // Get all tea entries for this period
            var allTeaEntries = await _context.TeaEntries
                .Where(t => t.PeriodId == periodId || (t.Date >= start && t.Date <= end))
                .ToListAsync();

            int successCount = 0;
            int failCount = 0;
            var failedUsers = new List<string>();

            foreach (var user in users)
            {
                try
                {
                    // Get user's attendance records
                    var attendances = await _context.Attendances
                        .Where(a => a.UserId == user.UserId && a.Date >= start && a.Date <= end)
                        .Include(a => a.BreakfastDishPlan)
                        .Include(a => a.LunchDishPlan)
                        .Include(a => a.DinnerDishPlan)
                        .ToListAsync();

                    // Calculate meal charges
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

                    // Calculate other charges
                    decimal waterCharges = period.FixedWaterCharge;
                    var userTeaEntries = allTeaEntries.Where(t => t.UserId == user.UserId).ToList();
                    int teaCups = userTeaEntries.Sum(t => t.Cups);
                    decimal teaCharges = teaCups * period.TeaPricePerCup;
                    decimal totalCharges = mealCharges + waterCharges + teaCharges;

                    // Calculate payments (only approved/completed)
                    var totalPaid = await _context.Payments
                        .Where(p => p.UserId == user.UserId && p.PeriodId == periodId &&
                               (p.Status == PaymentStatus.Approved || p.Status == PaymentStatus.Completed))
                        .SumAsync(p => (decimal?)p.Amount) ?? 0m;

                    decimal balance = totalCharges - totalPaid;

                    // Send email
                    await _emailService.SendPeriodBillStatementEmailAsync(
                        user.Email!,
                        user.FullName,
                        period,
                        breakfastCount,
                        lunchCount,
                        dinnerCount,
                        mealCharges,
                        waterCharges,
                        teaCups,
                        teaCharges,
                        totalCharges,
                        totalPaid,
                        balance
                    );

                    successCount++;
                    _logger.LogInformation("Bill statement email sent to {Email} for period {Period}", user.Email, period.PeriodName);
                }
                catch (Exception ex)
                {
                    failCount++;
                    failedUsers.Add(user.FullName);
                    _logger.LogError(ex, "Failed to send bill statement email to {Email}", user.Email);
                }
            }

            if (failCount == 0)
            {
                TempData["SuccessMessage"] = $"Bill statement emails sent successfully to {successCount} member(s)!";
            }
            else if (successCount > 0)
            {
                TempData["WarningMessage"] = $"Sent {successCount} email(s), but {failCount} failed: {string.Join(", ", failedUsers)}";
            }
            else
            {
                TempData["ErrorMessage"] = $"Failed to send all {failCount} email(s). Please check SMTP configuration.";
            }

            return RedirectToAction(nameof(Index), new { periodId });
        }

        // Send bill statement email to a single user
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendBillStatementEmail(int periodId, int userId)
        {
            var period = await _context.MessPeriods.FindAsync(periodId);
            if (period == null)
            {
                TempData["ErrorMessage"] = "Period not found.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrEmpty(user.Email))
            {
                TempData["ErrorMessage"] = $"No email address configured for {user.FullName}.";
                return RedirectToAction(nameof(Index), new { periodId });
            }

            var start = period.StartDate;
            var end = period.EndDate;

            try
            {
                // Get user's attendance records
                var attendances = await _context.Attendances
                    .Where(a => a.UserId == user.UserId && a.Date >= start && a.Date <= end)
                    .Include(a => a.BreakfastDishPlan)
                    .Include(a => a.LunchDishPlan)
                    .Include(a => a.DinnerDishPlan)
                    .ToListAsync();

                // Calculate meal charges
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

                // Calculate other charges
                decimal waterCharges = period.FixedWaterCharge;
                var teaEntries = await _context.TeaEntries
                    .Where(t => t.UserId == user.UserId && (t.PeriodId == periodId || (t.Date >= start && t.Date <= end)))
                    .ToListAsync();
                int teaCups = teaEntries.Sum(t => t.Cups);
                decimal teaCharges = teaCups * period.TeaPricePerCup;
                decimal totalCharges = mealCharges + waterCharges + teaCharges;

                // Calculate payments (only approved/completed)
                var totalPaid = await _context.Payments
                    .Where(p => p.UserId == user.UserId && p.PeriodId == periodId &&
                           (p.Status == PaymentStatus.Approved || p.Status == PaymentStatus.Completed))
                    .SumAsync(p => (decimal?)p.Amount) ?? 0m;

                decimal balance = totalCharges - totalPaid;

                // Send email
                await _emailService.SendPeriodBillStatementEmailAsync(
                    user.Email,
                    user.FullName,
                    period,
                    breakfastCount,
                    lunchCount,
                    dinnerCount,
                    mealCharges,
                    waterCharges,
                    teaCups,
                    teaCharges,
                    totalCharges,
                    totalPaid,
                    balance
                );

                TempData["SuccessMessage"] = $"Bill statement email sent successfully to {user.FullName}!";
                _logger.LogInformation("Bill statement email sent to {Email} for period {Period}", user.Email, period.PeriodName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send bill statement email to {Email}", user.Email);
                TempData["ErrorMessage"] = $"Failed to send email to {user.FullName}. Please check SMTP configuration.";
            }

            return RedirectToAction(nameof(Index), new { periodId });
        }
    }
}