using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MessManagement.Data;
using MessManagement.Models;

namespace MessManagement.Controllers
{
    [Authorize(Roles = "Admin")]
    public class MessPeriodController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MessPeriodController(ApplicationDbContext context) => _context = context;

        public async Task<IActionResult> Index() => View(await _context.MessPeriods.ToListAsync());

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.MessPeriods.FindAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("PeriodName,StartDate,EndDate,IsActive,FixedWaterCharge,TeaPricePerCup")] MessPeriod period)
        {
            if (!ModelState.IsValid) return View(period);
            
            // If this period is active, deactivate other periods
            if (period.IsActive)
            {
                var otherActivePeriods = await _context.MessPeriods.Where(p => p.IsActive).ToListAsync();
                foreach (var p in otherActivePeriods)
                {
                    p.IsActive = false;
                }
            }
            
            _context.Add(period);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Period '{period.PeriodName}' has been created successfully!";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.MessPeriods.FindAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("PeriodId,PeriodName,StartDate,EndDate,IsActive,FixedWaterCharge,TeaPricePerCup")] MessPeriod period)
        {
            if (id != period.PeriodId) return NotFound();
            if (!ModelState.IsValid) return View(period);
            try
            {
                // If this period is being set to active, deactivate other periods
                if (period.IsActive)
                {
                    var otherActivePeriods = await _context.MessPeriods.Where(p => p.IsActive && p.PeriodId != period.PeriodId).ToListAsync();
                    foreach (var p in otherActivePeriods)
                    {
                        p.IsActive = false;
                    }
                }
                
                _context.Update(period);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Period '{period.PeriodName}' has been updated successfully!";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.MessPeriods.Any(p => p.PeriodId == id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.MessPeriods.FindAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _context.MessPeriods.FindAsync(id);
            if (item != null)
            {
                var periodName = item.PeriodName;
                _context.MessPeriods.Remove(item);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Period '{periodName}' has been deleted successfully!";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
