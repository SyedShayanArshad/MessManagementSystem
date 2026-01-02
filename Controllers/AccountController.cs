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

            // Case-insensitive search for the username
            var loweredUsername = username.ToLower();
            
            // First check if user exists at all (regardless of active status)
            var userExists = await _db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == loweredUsername);
            if (userExists == null)
            {
                ModelState.AddModelError(string.Empty, "Username not found. Please check your username and try again.");
                return View(model);
            }
            
            // Check if user is active
            if (!userExists.IsActive)
            {
                ModelState.AddModelError(string.Empty, "This account has been deactivated. Please contact the administrator.");
                return View(model);
            }

            var verification = _passwordHasher.VerifyHashedPassword(userExists, userExists.PasswordHash, password);
            if (verification == PasswordVerificationResult.Success)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, userExists.UserId.ToString()),
                    new Claim(ClaimTypes.Name, userExists.Username),
                    new Claim(ClaimTypes.Role, userExists.Role)
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(claimsIdentity);

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError(string.Empty, "Incorrect password. Please try again.");
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
