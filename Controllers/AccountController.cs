using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MessManagement.Data;
using MessManagement.Models;
using MessManagement.ViewModels;
using MessManagement.Services;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MessManagement.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly PasswordHasher<User> _passwordHasher;
        private readonly IEmailService _emailService;

        public AccountController(ApplicationDbContext db, IEmailService emailService)
        {
            _db = db;
            _passwordHasher = new PasswordHasher<User>();
            _emailService = emailService;
        }

        public IActionResult Login() => View(new LoginViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var username = model.Username?.Trim() ?? string.Empty;
            var password = model.Password ?? string.Empty;

            // Case-insensitive search for the username and ensure user is active
            var loweredUsername = username.ToLower();
            var user = await _db.Users.FirstOrDefaultAsync(u => u.IsActive && u.Username.ToLower() == loweredUsername);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return View();
            }

            var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
            if (verification == PasswordVerificationResult.Success)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Role, user.Role)
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(claimsIdentity);

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        public IActionResult AccessDenied() => View();

        // GET: /Account/ForgotPassword
        public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

        // POST: /Account/ForgotPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var email = model.Email?.Trim().ToLower() ?? string.Empty;
            var user = await _db.Users.FirstOrDefaultAsync(u => u.IsActive && u.Email != null && u.Email.ToLower() == email);

            // Always show success message to prevent email enumeration
            if (user == null)
            {
                TempData["SuccessMessage"] = "If an account with that email exists, we've sent password reset instructions.";
                return RedirectToAction(nameof(ForgotPasswordConfirmation));
            }

            // Generate secure token
            var token = GenerateSecureToken();
            user.PasswordResetToken = token;
            user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);
            await _db.SaveChangesAsync();

            // Generate reset link
            var resetLink = Url.Action("ResetPassword", "Account", 
                new { token = token, email = user.Email }, Request.Scheme);

            try
            {
                await _emailService.SendPasswordResetEmailAsync(user.Email!, resetLink!, user.FullName);
                TempData["SuccessMessage"] = "If an account with that email exists, we've sent password reset instructions.";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "Failed to send reset email. Please try again later.";
                return View(model);
            }

            return RedirectToAction(nameof(ForgotPasswordConfirmation));
        }

        // GET: /Account/ForgotPasswordConfirmation
        public IActionResult ForgotPasswordConfirmation() => View();

        // GET: /Account/ResetPassword
        public IActionResult ResetPassword(string? token, string? email)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "Invalid password reset link.";
                return RedirectToAction(nameof(Login));
            }

            var model = new ResetPasswordViewModel
            {
                Token = token,
                Email = email
            };
            return View(model);
        }

        // POST: /Account/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var email = model.Email?.Trim().ToLower() ?? string.Empty;
            var user = await _db.Users.FirstOrDefaultAsync(u => 
                u.IsActive && 
                u.Email != null && 
                u.Email.ToLower() == email &&
                u.PasswordResetToken == model.Token &&
                u.PasswordResetTokenExpiry > DateTime.UtcNow);

            if (user == null)
            {
                TempData["ErrorMessage"] = "Invalid or expired reset link. Please request a new one.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            // Update password
            user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiry = null;
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Your password has been reset successfully. You can now log in with your new password.";
            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }

        // GET: /Account/ResetPasswordConfirmation
        public IActionResult ResetPasswordConfirmation() => View();

        private static string GenerateSecureToken()
        {
            var bytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }
    }
}
