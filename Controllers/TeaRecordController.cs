using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MessManagement.Data;
using MessManagement.Models;

namespace MessManagement.Controllers
{
    [Authorize(Roles = "Admin")]
    public class TeaRecordController : Controller
    {
        private readonly ApplicationDbContext _context;
        public TeaRecordController(ApplicationDbContext context) => _context = context;

        public async Task<IActionResult> Index() 
        {
            var records = await _context.TeaRecords
                .Include(t => t.MessPeriod)
                .OrderByDescending(t => t.Date)
                .ToListAsync();
            return View(records);
        }

        public async Task<IActionResult> Create()
        {
            ViewBag.Periods = await _context.MessPeriods.OrderByDescending(p => p.StartDate).ToListAsync();
            var activePeriod = await _context.MessPeriods.FirstOrDefaultAsync(p => p.IsActive);
            var record = new TeaRecord
            {
                Date = DateTime.Today,
                PeriodId = activePeriod?.PeriodId
            };
            return View(record);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("PeriodId,Date,TotalCupsServed,Remarks")] TeaRecord teaRecord)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Periods = await _context.MessPeriods.OrderByDescending(p => p.StartDate).ToListAsync();
                return View(teaRecord);
            }
            _context.TeaRecords.Add(teaRecord);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Tea record added successfully!";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.TeaRecords.FindAsync(id);
            if (item == null) return NotFound();
            ViewBag.Periods = await _context.MessPeriods.OrderByDescending(p => p.StartDate).ToListAsync();
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("TeaRecordId,PeriodId,Date,TotalCupsServed,Remarks")] TeaRecord teaRecord)
        {
            if (id != teaRecord.TeaRecordId) return NotFound();
            if (!ModelState.IsValid)
            {
                ViewBag.Periods = await _context.MessPeriods.OrderByDescending(p => p.StartDate).ToListAsync();
                return View(teaRecord);
            }
            try
            {
                _context.Update(teaRecord);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Tea record updated successfully!";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.TeaRecords.Any(t => t.TeaRecordId == id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.TeaRecords.Include(t => t.MessPeriod).FirstOrDefaultAsync(t => t.TeaRecordId == id);
            if (item == null) return NotFound();
            return View(item);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _context.TeaRecords.FindAsync(id);
            if (item != null) _context.TeaRecords.Remove(item);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Tea record deleted successfully!";
            return RedirectToAction(nameof(Index));
        }
    }
}
