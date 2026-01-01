using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MessManagement.Data;
using MessManagement.Models;

namespace MessManagement.Controllers
{
    [Authorize(Roles = "Admin")]
    public class DishPlanController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DishPlanController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: DishPlan
        public async Task<IActionResult> Index() => View(await _context.DishPlans.ToListAsync());

        // GET: DishPlan/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.DishPlans.FindAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }

        // GET: DishPlan/Create
        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("DayOfWeek,MealType,DishName,Price,Notes")] DishPlan dishPlan)
        {
            if (!ModelState.IsValid) return View(dishPlan);
            _context.Add(dishPlan);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Dish '{dishPlan.DishName}' has been created successfully!";
            return RedirectToAction(nameof(Index));
        }

        // GET: DishPlan/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.DishPlans.FindAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("DishPlanId,DayOfWeek,MealType,DishName,Price,Notes")] DishPlan dishPlan)
        {
            if (id != dishPlan.DishPlanId) return NotFound();
            if (!ModelState.IsValid) return View(dishPlan);
            try
            {
                _context.Update(dishPlan);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Dish '{dishPlan.DishName}' has been updated successfully!";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DishPlanExists(dishPlan.DishPlanId)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: DishPlan/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.DishPlans.FindAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _context.DishPlans.FindAsync(id);
            if (item != null)
            {
                var dishName = item.DishName;
                _context.DishPlans.Remove(item);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Dish '{dishName}' has been deleted successfully!";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool DishPlanExists(int id) => _context.DishPlans.Any(e => e.DishPlanId == id);
    }
}
