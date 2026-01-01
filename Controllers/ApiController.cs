using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MessManagement.Data;
using MessManagement.Models;
using MessManagement.Services;

namespace MessManagement.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IJwtService _jwtService;
        private readonly PasswordHasher<User> _passwordHasher;

        public ApiController(ApplicationDbContext context, IJwtService jwtService)
        {
            _context = context;
            _jwtService = jwtService;
            _passwordHasher = new PasswordHasher<User>();
        }

        #region Authentication

        /// <summary>
        /// JWT Login endpoint - Returns JWT token for API authentication
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request)
        {
            if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new { success = false, message = "Username and password are required" });
            }

            var username = request.Username.Trim().ToLower();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.IsActive && u.Username.ToLower() == username);

            if (user == null)
            {
                return Unauthorized(new { success = false, message = "Invalid credentials" });
            }

            var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (verification != PasswordVerificationResult.Success)
            {
                return Unauthorized(new { success = false, message = "Invalid credentials" });
            }

            var token = _jwtService.GenerateToken(user);

            return Ok(new
            {
                success = true,
                token = token,
                user = new
                {
                    userId = user.UserId,
                    username = user.Username,
                    fullName = user.FullName,
                    role = user.Role
                }
            });
        }

        #endregion

        #region Dashboard Stats (AJAX/Fetch)

        /// <summary>
        /// Get dashboard statistics - used by fetch to avoid page refresh
        /// </summary>
        [HttpGet("dashboard/stats")]
        [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},Cookies")]
        public async Task<IActionResult> GetDashboardStatsAsync()
        {
            var today = DateTime.Today;
            var activeMembers = await _context.Users.CountAsync(u => u.IsActive);
            var todayAttendance = await _context.Attendances.CountAsync(a => a.Date == today && a.IsPresent);
            var activePeriod = await _context.MessPeriods.FirstOrDefaultAsync(p => p.IsActive);
            
            decimal totalPayments = 0;
            int teaServed = 0;

            if (activePeriod != null)
            {
                totalPayments = await _context.Payments
                    .Where(p => p.PeriodId == activePeriod.PeriodId)
                    .SumAsync(p => p.Amount);

                teaServed = await _context.TeaRecords
                    .Where(t => t.PeriodId == activePeriod.PeriodId)
                    .SumAsync(t => t.TotalCupsServed);
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    activeMembers,
                    todayAttendance,
                    totalPayments,
                    teaServed,
                    periodName = activePeriod?.PeriodName ?? "No Active Period"
                }
            });
        }

        #endregion

        #region Attendance API (Fetch/AJAX)

        /// <summary>
        /// Get attendance for a specific date
        /// </summary>
        [HttpGet("attendance")]
        [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},Cookies")]
        public async Task<IActionResult> GetAttendanceAsync([FromQuery] DateTime? date)
        {
            var modelDate = date ?? DateTime.Today;
            var users = await _context.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.FullName)
                .Select(u => new { u.UserId, u.FullName, u.Username })
                .ToListAsync();

            var attendances = await _context.Attendances
                .Where(a => a.Date == modelDate)
                .Include(a => a.DishPlan)
                .ToListAsync();

            var dishPlans = await _context.DishPlans
                .Where(d => d.DayOfWeek == modelDate.DayOfWeek.ToString())
                .Select(d => new { d.DishPlanId, d.DishName, d.MealType, d.Price })
                .ToListAsync();

            var result = users.Select(u =>
            {
                var att = attendances.FirstOrDefault(a => a.UserId == u.UserId);
                return new
                {
                    u.UserId,
                    u.FullName,
                    u.Username,
                    IsPresent = att?.IsPresent ?? true,
                    DishPlanId = att?.DishPlanId,
                    DishName = att?.DishPlan?.DishName
                };
            });

            return Ok(new
            {
                success = true,
                date = modelDate.ToString("yyyy-MM-dd"),
                dishPlans,
                attendance = result
            });
        }

        /// <summary>
        /// Toggle attendance for a user (AJAX call to avoid page refresh)
        /// </summary>
        [HttpPost("attendance/toggle")]
        [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},Cookies", Roles = "Admin")]
        public async Task<IActionResult> ToggleAttendanceAsync([FromBody] ToggleAttendanceRequest request)
        {
            var attendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.UserId == request.UserId && a.Date == request.Date);

            if (attendance == null)
            {
                var defaultDish = await _context.DishPlans
                    .FirstOrDefaultAsync(d => d.DayOfWeek == request.Date.DayOfWeek.ToString());

                attendance = new Attendance
                {
                    UserId = request.UserId,
                    Date = request.Date,
                    IsPresent = request.IsPresent,
                    DishPlanId = request.DishPlanId ?? defaultDish?.DishPlanId
                };
                _context.Attendances.Add(attendance);
            }
            else
            {
                attendance.IsPresent = request.IsPresent;
                if (request.DishPlanId.HasValue)
                    attendance.DishPlanId = request.DishPlanId;
                _context.Attendances.Update(attendance);
            }

            await _context.SaveChangesAsync();

            // Handle auto-charge payment
            if (attendance.IsPresent)
            {
                await AddPaymentForAttendanceAsync(attendance);
            }
            else
            {
                await RemovePaymentForAttendanceAsync(attendance);
            }

            return Ok(new
            {
                success = true,
                message = attendance.IsPresent ? "Marked as present" : "Marked as absent",
                attendance = new
                {
                    attendance.AttendanceId,
                    attendance.UserId,
                    attendance.Date,
                    attendance.IsPresent,
                    attendance.DishPlanId
                }
            });
        }

        /// <summary>
        /// Bulk save attendance (AJAX)
        /// </summary>
        [HttpPost("attendance/bulk-save")]
        [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},Cookies", Roles = "Admin")]
        public async Task<IActionResult> BulkSaveAttendanceAsync([FromBody] BulkAttendanceRequest request)
        {
            int updated = 0;
            int created = 0;

            foreach (var item in request.Items)
            {
                var attendance = await _context.Attendances
                    .FirstOrDefaultAsync(a => a.UserId == item.UserId && a.Date == request.Date);

                if (attendance == null)
                {
                    attendance = new Attendance
                    {
                        UserId = item.UserId,
                        Date = request.Date,
                        IsPresent = item.IsPresent,
                        DishPlanId = item.DishPlanId
                    };
                    _context.Attendances.Add(attendance);
                    created++;

                    if (attendance.IsPresent)
                    {
                        await _context.SaveChangesAsync(); // Save to get ID
                        await AddPaymentForAttendanceAsync(attendance);
                    }
                }
                else
                {
                    var wasPresent = attendance.IsPresent;
                    attendance.IsPresent = item.IsPresent;
                    attendance.DishPlanId = item.DishPlanId;
                    _context.Attendances.Update(attendance);
                    updated++;

                    if (!wasPresent && attendance.IsPresent)
                    {
                        await AddPaymentForAttendanceAsync(attendance);
                    }
                    else if (wasPresent && !attendance.IsPresent)
                    {
                        await RemovePaymentForAttendanceAsync(attendance);
                    }
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = $"Attendance saved. Created: {created}, Updated: {updated}",
                created,
                updated
            });
        }

        #endregion

        #region Users API

        /// <summary>
        /// Get all users (for AJAX loading)
        /// </summary>
        [HttpGet("users")]
        [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},Cookies", Roles = "Admin")]
        public async Task<IActionResult> GetUsersAsync()
        {
            var users = await _context.Users
                .Select(u => new
                {
                    u.UserId,
                    u.FullName,
                    u.Username,
                    u.Role,
                    u.IsActive,
                    u.PhoneNumber
                })
                .ToListAsync();

            return Ok(new { success = true, data = users });
        }

        /// <summary>
        /// Delete user via API
        /// </summary>
        [HttpDelete("users/{id}")]
        [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},Cookies", Roles = "Admin")]
        public async Task<IActionResult> DeleteUserAsync(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found" });
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "User deleted successfully" });
        }

        #endregion

        #region Helper Methods

        private async Task AddPaymentForAttendanceAsync(Attendance attendance)
        {
            var dish = attendance.DishPlanId.HasValue
                ? await _context.DishPlans.FindAsync(attendance.DishPlanId.Value)
                : await _context.DishPlans.FirstOrDefaultAsync(d => d.DayOfWeek == attendance.Date.DayOfWeek.ToString());

            var amount = dish?.Price ?? 0m;
            if (amount <= 0) return;

            var period = await _context.MessPeriods
                .FirstOrDefaultAsync(p => p.StartDate <= attendance.Date && p.EndDate >= attendance.Date)
                ?? await _context.MessPeriods.FirstOrDefaultAsync(p => p.IsActive);

            var existing = await _context.Payments
                .FirstOrDefaultAsync(p => p.AttendanceId == attendance.AttendanceId && p.PaymentMethod == "Auto");

            if (existing != null)
            {
                existing.Amount = amount;
                _context.Payments.Update(existing);
            }
            else
            {
                var payment = new Payment
                {
                    UserId = attendance.UserId,
                    PeriodId = period?.PeriodId,
                    AttendanceId = attendance.AttendanceId,
                    Amount = amount,
                    PaymentDate = DateTime.Now,
                    PaymentMethod = "Auto",
                    Remarks = $"Auto-charge for attendance on {attendance.Date:yyyy-MM-dd}"
                };
                _context.Payments.Add(payment);
            }

            await _context.SaveChangesAsync();
        }

        private async Task RemovePaymentForAttendanceAsync(Attendance attendance)
        {
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.AttendanceId == attendance.AttendanceId && p.PaymentMethod == "Auto");

            if (payment != null)
            {
                _context.Payments.Remove(payment);
                await _context.SaveChangesAsync();
            }
        }

        #endregion
    }

    #region Request DTOs

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class ToggleAttendanceRequest
    {
        public int UserId { get; set; }
        public DateTime Date { get; set; }
        public bool IsPresent { get; set; }
        public int? DishPlanId { get; set; }
    }

    public class BulkAttendanceRequest
    {
        public DateTime Date { get; set; }
        public List<AttendanceItemRequest> Items { get; set; } = new();
    }

    public class AttendanceItemRequest
    {
        public int UserId { get; set; }
        public bool IsPresent { get; set; }
        public int? DishPlanId { get; set; }
    }

    #endregion
}
