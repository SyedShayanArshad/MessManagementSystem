using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MessManagement.Data;
using MessManagement.Models;
using MessManagement.Models.ViewModels;

namespace MessManagement.Controllers
{
    [Authorize]
    public class TeaController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TeaController(ApplicationDbContext context) => _context = context;

        /// <summary>
        /// Admin: Tea management dashboard with period-based calendar and mark tea functionality
        /// </summary>
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index(DateTime? date, int? periodId)
        {
            var modelDate = date ?? DateTime.Today;
            
            var users = await _context.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
            var periods = await _context.MessPeriods.OrderByDescending(p => p.StartDate).ToListAsync();

            // Get current/active period or selected period
            MessPeriod? selectedPeriod = null;
            if (periodId.HasValue)
            {
                selectedPeriod = periods.FirstOrDefault(p => p.PeriodId == periodId);
            }
            selectedPeriod ??= periods.FirstOrDefault(p => p.StartDate <= DateTime.Today && p.EndDate >= DateTime.Today) 
                              ?? periods.FirstOrDefault();
            
            var currentPeriod = periods.FirstOrDefault(p => p.StartDate <= DateTime.Today && p.EndDate >= DateTime.Today);
            
            // ============================================
            // DASHBOARD DATA
            // ============================================
            
            ViewBag.AllPeriods = periods;
            ViewBag.SelectedPeriod = selectedPeriod;
            ViewBag.CurrentPeriod = currentPeriod;
            ViewBag.TotalMembers = users.Count;
            
            // Period-based calendar data
            var periodCalendarData = new Dictionary<DateTime, TeaCalendarDayData>();
            if (selectedPeriod != null)
            {
                var periodEntries = await _context.TeaEntries
                    .Where(t => t.Date >= selectedPeriod.StartDate && t.Date <= selectedPeriod.EndDate)
                    .ToListAsync();
                
                for (var d = selectedPeriod.StartDate; d <= selectedPeriod.EndDate; d = d.AddDays(1))
                {
                    var dayEntries = periodEntries.Where(t => t.Date == d).ToList();
                    periodCalendarData[d] = new TeaCalendarDayData
                    {
                        TotalCups = dayEntries.Sum(t => t.Cups),
                        MembersWithTea = dayEntries.Count(t => t.Cups > 0),
                        VerifiedCount = dayEntries.Count(t => t.VerifiedByUser),
                        TotalEntries = dayEntries.Count,
                        HasData = dayEntries.Any()
                    };
                }
            }
            ViewBag.PeriodCalendarData = periodCalendarData;
            
            // Today's tea stats
            var todayEntries = await _context.TeaEntries.Where(t => t.Date == DateTime.Today).ToListAsync();
            ViewBag.TodayTotalCups = todayEntries.Sum(t => t.Cups);
            ViewBag.TodayMembersWithTea = todayEntries.Count(t => t.Cups > 0);
            ViewBag.TeaPricePerCup = selectedPeriod?.TeaPricePerCup ?? currentPeriod?.TeaPricePerCup ?? 0;
            
            // Period totals
            if (selectedPeriod != null)
            {
                var periodEntries = await _context.TeaEntries
                    .Where(t => t.Date >= selectedPeriod.StartDate && t.Date <= selectedPeriod.EndDate)
                    .ToListAsync();
                ViewBag.PeriodTotalCups = periodEntries.Sum(t => t.Cups);
                ViewBag.PeriodTotalCost = periodEntries.Sum(t => t.Cups) * selectedPeriod.TeaPricePerCup;
                ViewBag.PeriodVerifiedCount = periodEntries.Count(t => t.VerifiedByUser);
                ViewBag.PeriodTotalEntries = periodEntries.Count;
            }
            
            // Pending verifications
            var pendingVerifications = await _context.TeaEntries
                .Where(t => !t.VerifiedByUser && t.Cups > 0)
                .CountAsync();
            ViewBag.PendingVerifications = pendingVerifications;
            
            // Recent pending (for panel) - get most recent 10
            var recentPending = await _context.TeaEntries
                .Include(t => t.User)
                .Where(t => !t.VerifiedByUser && t.Cups > 0)
                .OrderByDescending(t => t.Date)
                .Take(10)
                .ToListAsync();
            ViewBag.RecentPending = recentPending;
            
            // Check if selected date is editable
            var isEditable = modelDate >= DateTime.Today || 
                            (currentPeriod != null && modelDate >= currentPeriod.StartDate && modelDate <= currentPeriod.EndDate);
            ViewBag.IsEditable = isEditable;
            
            // ============================================
            // TEA MARK VIEW MODEL FOR SELECTED DATE
            // ============================================
            
            var existingEntries = await _context.TeaEntries
                .Where(t => t.Date == modelDate)
                .ToListAsync();
            
            // Check attendance for reference
            var attendances = await _context.Attendances
                .Where(a => a.Date == modelDate)
                .ToListAsync();
            
            var vm = new TeaMarkViewModel
            {
                Date = modelDate,
                PeriodId = selectedPeriod?.PeriodId,
                PeriodName = selectedPeriod?.PeriodName
            };
            
            foreach (var user in users)
            {
                var entry = existingEntries.FirstOrDefault(e => e.UserId == user.UserId);
                var hasAttendance = attendances.Any(a => a.UserId == user.UserId && 
                    (a.IsBreakfastPresent || a.IsLunchPresent || a.IsDinnerPresent));
                
                vm.Items.Add(new TeaMarkItemViewModel
                {
                    UserId = user.UserId,
                    FullName = user.FullName,
                    Cups = entry?.Cups ?? 0,
                    Remarks = entry?.Remarks,
                    VerifiedByUser = entry?.VerifiedByUser ?? false,
                    TeaEntryId = entry?.TeaEntryId,
                    HasMealAttendance = hasAttendance
                });
            }
            
            return View(vm);
        }

        /// <summary>
        /// Admin: Mark tea for all members on a specific date (legacy route, redirects to Index)
        /// </summary>
        [Authorize(Roles = "Admin")]
        public IActionResult MarkTea(DateTime? date)
        {
            return RedirectToAction(nameof(Index), new { date = date?.ToString("yyyy-MM-dd") });
        }

        /// <summary>
        /// Admin: Save tea entries for all members on a date
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveTea(TeaMarkViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                var activePeriod = await _context.MessPeriods.FirstOrDefaultAsync(p => p.IsActive);
                ViewBag.TeaPricePerCup = activePeriod?.TeaPricePerCup ?? 0;
                return View(vm);
            }

            var date = vm.Date.Date;

            foreach (var item in vm.Items)
            {
                var entry = await _context.TeaEntries
                    .FirstOrDefaultAsync(t => t.UserId == item.UserId && t.Date == date);

                if (entry == null)
                {
                    // Create new entry
                    entry = new TeaEntry
                    {
                        UserId = item.UserId,
                        Date = date,
                        PeriodId = vm.PeriodId,
                        Cups = item.Cups,
                        Remarks = item.Remarks,
                        VerifiedByUser = false
                    };
                    _context.TeaEntries.Add(entry);
                }
                else
                {
                    // Update existing entry
                    // Only reset verification if cups changed
                    if (entry.Cups != item.Cups)
                    {
                        entry.VerifiedByUser = false;
                        entry.VerifiedOn = null;
                    }
                    entry.Cups = item.Cups;
                    entry.Remarks = item.Remarks;
                    entry.PeriodId = vm.PeriodId;
                    _context.TeaEntries.Update(entry);
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Tea entries for {date:MMM dd, yyyy} saved successfully!";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Member: View their own tea entries
        /// </summary>
        public async Task<IActionResult> MyTea()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return NotFound();

            var activePeriod = await _context.MessPeriods.FirstOrDefaultAsync(p => p.IsActive);

            var entries = await _context.TeaEntries
                .Where(t => t.UserId == user.UserId)
                .Include(t => t.MessPeriod)
                .OrderByDescending(t => t.Date)
                .ToListAsync();

            // Calculate charges
            if (activePeriod != null)
            {
                foreach (var entry in entries)
                {
                    var period = entry.MessPeriod ?? activePeriod;
                    entry.TeaCharge = entry.Cups * period.TeaPricePerCup;
                }
            }

            // Get all mess periods for grouping
            var messPeriods = await _context.MessPeriods
                .OrderByDescending(p => p.StartDate)
                .ToListAsync();
            ViewBag.MessPeriods = messPeriods;

            ViewBag.User = user;
            ViewBag.TeaPricePerCup = activePeriod?.TeaPricePerCup ?? 0;
            return View(entries);
        }

        /// <summary>
        /// Member: Verify a tea entry
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify(int id)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return NotFound();

            var entry = await _context.TeaEntries.FirstOrDefaultAsync(t => t.TeaEntryId == id && t.UserId == user.UserId);
            if (entry == null) return NotFound();

            entry.VerifiedByUser = true;
            entry.VerifiedOn = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Tea entry for {entry.Date:MMM dd, yyyy} verified successfully!";
            return RedirectToAction(nameof(MyTea));
        }

        /// <summary>
        /// Member: Verify all pending tea entries at once
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyAll()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return NotFound();

            var pendingEntries = await _context.TeaEntries
                .Where(t => t.UserId == user.UserId && !t.VerifiedByUser)
                .ToListAsync();

            foreach (var entry in pendingEntries)
            {
                entry.VerifiedByUser = true;
                entry.VerifiedOn = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"{pendingEntries.Count} tea entries verified successfully!";
            return RedirectToAction(nameof(MyTea));
        }

        /// <summary>
        /// Admin: Delete a tea entry
        /// </summary>
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var entry = await _context.TeaEntries
                .Include(t => t.User)
                .Include(t => t.MessPeriod)
                .FirstOrDefaultAsync(t => t.TeaEntryId == id);

            if (entry == null) return NotFound();

            return View(entry);
        }

        /// <summary>
        /// Admin: Confirm delete tea entry
        /// </summary>
        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var entry = await _context.TeaEntries.FindAsync(id);
            if (entry != null)
            {
                _context.TeaEntries.Remove(entry);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Tea entry deleted successfully!";
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Admin: Quick action to set all present members to 1 cup
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetFromAttendance(DateTime date, int? periodId)
        {
            var attendances = await _context.Attendances
                .Where(a => a.Date == date && (a.IsBreakfastPresent || a.IsLunchPresent || a.IsDinnerPresent))
                .ToListAsync();

            foreach (var att in attendances)
            {
                var entry = await _context.TeaEntries
                    .FirstOrDefaultAsync(t => t.UserId == att.UserId && t.Date == date);

                if (entry == null)
                {
                    entry = new TeaEntry
                    {
                        UserId = att.UserId,
                        Date = date,
                        PeriodId = periodId,
                        Cups = 1,
                        VerifiedByUser = false
                    };
                    _context.TeaEntries.Add(entry);
                }
                else if (entry.Cups == 0)
                {
                    entry.Cups = 1;
                    entry.VerifiedByUser = false;
                    entry.VerifiedOn = null;
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Tea set to 1 cup for {attendances.Count} present members!";
            return RedirectToAction(nameof(MarkTea), new { date = date.ToString("yyyy-MM-dd") });
        }
    }
}
