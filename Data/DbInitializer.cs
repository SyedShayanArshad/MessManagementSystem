using MessManagement.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MessManagement.Data
{
    public static class DbInitializer
    {
        public static void Initialize(ApplicationDbContext context)
        {
            context.Database.Migrate();
            // Add Admin user
            if (!context.Users.Any(u => u.Role == "Admin"))
            {
                var passwordHasher = new PasswordHasher<User>();
                var admin = new User
                {
                    FullName = "Administrator",
                    Username = "admin",
                    Role = "Admin",
                    IsActive = true,
                    PhoneNumber = "000-000-0000"
                };
                admin.PasswordHash = passwordHasher.HashPassword(admin, "Admin@123");
                context.Users.Add(admin);
                context.SaveChanges();
            }
        }
    }
}
