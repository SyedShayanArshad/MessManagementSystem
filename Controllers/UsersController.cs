using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MessManagement.Data;
using MessManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace MessManagement.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PasswordHasher<User> _passwordHasher;

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<User>();
        }

        // GET: Users
        public async Task<IActionResult> Index()
        {
            return View(await _context.Users.ToListAsync());
        }

        // GET: Users/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var user = await _context.Users.FirstOrDefaultAsync(m => m.UserId == id);
            if (user == null) return NotFound();
            return View(user);
        }

        // GET: Users/Create
        public IActionResult Create() => View();

        // POST: Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FullName,Username,Role,PhoneNumber,Email")] User user, string password)
        {
            if (!ModelState.IsValid) return View(user);

            // hash given password
            if (string.IsNullOrWhiteSpace(password))
            {
                // Add model error to the password field name used in the form
                ModelState.AddModelError("password", "Password is required.");
                return View(user);
            }

            user.Username = user.Username.Trim();
            // Normalize username to avoid case-sensitivity issues
            user.Username = user.Username.ToLower();

            // Ensure username is unique
            if (_context.Users.Any(u => u.Username == user.Username))
            {
                ModelState.AddModelError("Username", "Username already exists.");
                return View(user);
            }

            user.PasswordHash = _passwordHasher.HashPassword(user, password);
            _context.Add(user);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Member '{user.FullName}' has been created successfully!";
            return RedirectToAction(nameof(Index));
        }

        // GET: Users/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("UserId,FullName,Username,Role,IsActive,PhoneNumber,Email")] User user, string? newPassword = null)
        {
            if (id != user.UserId) return NotFound();
            if (!ModelState.IsValid) return View(user);

            try
            {
                var existing = await _context.Users.FindAsync(id);
                if (existing == null) return NotFound();

                existing.FullName = user.FullName;
                var newUsername = user.Username.Trim().ToLower();
                // Check if another user already uses this username
                if (_context.Users.Any(u => u.UserId != id && u.Username == newUsername))
                {
                    ModelState.AddModelError("Username", "Username already exists.");
                    return View(user);
                }
                existing.Username = newUsername;
                existing.Role = user.Role;
                existing.IsActive = user.IsActive;
                existing.PhoneNumber = user.PhoneNumber;
                existing.Email = user.Email?.Trim();

                if (!string.IsNullOrWhiteSpace(newPassword))
                {
                    existing.PasswordHash = _passwordHasher.HashPassword(existing, newPassword);
                }
                _context.Update(existing);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UserExists(user.UserId)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: Users/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var user = await _context.Users.FirstOrDefaultAsync(m => m.UserId == id);
            if (user == null) return NotFound();
            return View(user);
        }

        // POST: Users/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, string confirmUsername)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            
            // Verify username confirmation
            if (string.IsNullOrWhiteSpace(confirmUsername) || 
                !confirmUsername.Equals(user.Username, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Username confirmation does not match. Please enter the correct username to delete.";
                return RedirectToAction(nameof(Delete), new { id });
            }
            
            var memberName = user.FullName;
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Member '{memberName}' has been deleted successfully!";
            return RedirectToAction(nameof(Index));
        }

        private bool UserExists(int id) => _context.Users.Any(e => e.UserId == id);
    }
}
