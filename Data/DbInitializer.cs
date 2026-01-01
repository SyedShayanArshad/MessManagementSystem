using MessManagement.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MessManagement.Data
{
    public static class DbInitializer
    {
        public static void Initialize(ApplicationDbContext context)
        {
            // Ensure database is created
            context.Database.EnsureCreated();

            // Add AttendanceId column to Payment table if it doesn't exist
            try
            {
                context.Database.ExecuteSqlRaw(@"
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Payment]') AND name = 'AttendanceId')
                    BEGIN
                        ALTER TABLE [dbo].[Payment] ADD [AttendanceId] INT NULL;
                        ALTER TABLE [dbo].[Payment] ADD CONSTRAINT [FK_Payment_Attendance_AttendanceId] 
                            FOREIGN KEY ([AttendanceId]) REFERENCES [dbo].[Attendance]([AttendanceId]);
                    END
                ");
            }
            catch
            {
                // Column might already exist or table structure different, continue
            }

            // Add new meal-type columns to Attendance table for Breakfast/Lunch/Dinner support
            try
            {
                context.Database.ExecuteSqlRaw(@"
                    -- Add IsBreakfastPresent column
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Attendance]') AND name = 'IsBreakfastPresent')
                    BEGIN
                        ALTER TABLE [dbo].[Attendance] ADD [IsBreakfastPresent] BIT NOT NULL DEFAULT 0;
                    END
                    
                    -- Add IsLunchPresent column
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Attendance]') AND name = 'IsLunchPresent')
                    BEGIN
                        ALTER TABLE [dbo].[Attendance] ADD [IsLunchPresent] BIT NOT NULL DEFAULT 1;
                    END
                    
                    -- Add IsDinnerPresent column
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Attendance]') AND name = 'IsDinnerPresent')
                    BEGIN
                        ALTER TABLE [dbo].[Attendance] ADD [IsDinnerPresent] BIT NOT NULL DEFAULT 1;
                    END
                    
                    -- Add BreakfastDishPlanId column
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Attendance]') AND name = 'BreakfastDishPlanId')
                    BEGIN
                        ALTER TABLE [dbo].[Attendance] ADD [BreakfastDishPlanId] INT NULL;
                    END
                    
                    -- Add LunchDishPlanId column
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Attendance]') AND name = 'LunchDishPlanId')
                    BEGIN
                        ALTER TABLE [dbo].[Attendance] ADD [LunchDishPlanId] INT NULL;
                    END
                    
                    -- Add DinnerDishPlanId column
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Attendance]') AND name = 'DinnerDishPlanId')
                    BEGIN
                        ALTER TABLE [dbo].[Attendance] ADD [DinnerDishPlanId] INT NULL;
                    END
                    
                    -- Add BreakfastVerified column
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Attendance]') AND name = 'BreakfastVerified')
                    BEGIN
                        ALTER TABLE [dbo].[Attendance] ADD [BreakfastVerified] BIT NOT NULL DEFAULT 0;
                    END
                    
                    -- Add LunchVerified column
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Attendance]') AND name = 'LunchVerified')
                    BEGIN
                        ALTER TABLE [dbo].[Attendance] ADD [LunchVerified] BIT NOT NULL DEFAULT 0;
                    END
                    
                    -- Add DinnerVerified column
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Attendance]') AND name = 'DinnerVerified')
                    BEGIN
                        ALTER TABLE [dbo].[Attendance] ADD [DinnerVerified] BIT NOT NULL DEFAULT 0;
                    END
                    
                    -- Add BreakfastVerifiedOn column
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Attendance]') AND name = 'BreakfastVerifiedOn')
                    BEGIN
                        ALTER TABLE [dbo].[Attendance] ADD [BreakfastVerifiedOn] DATETIME2 NULL;
                    END
                    
                    -- Add LunchVerifiedOn column
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Attendance]') AND name = 'LunchVerifiedOn')
                    BEGIN
                        ALTER TABLE [dbo].[Attendance] ADD [LunchVerifiedOn] DATETIME2 NULL;
                    END
                    
                    -- Add DinnerVerifiedOn column
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Attendance]') AND name = 'DinnerVerifiedOn')
                    BEGIN
                        ALTER TABLE [dbo].[Attendance] ADD [DinnerVerifiedOn] DATETIME2 NULL;
                    END
                ");
            }
            catch
            {
                // Columns might already exist, continue
            }

            // Create TeaEntry table for per-user tea consumption tracking
            try
            {
                context.Database.ExecuteSqlRaw(@"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TeaEntry')
                    BEGIN
                        CREATE TABLE [dbo].[TeaEntry] (
                            [TeaEntryId] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                            [UserId] INT NOT NULL,
                            [PeriodId] INT NULL,
                            [Date] DATETIME2 NOT NULL,
                            [Cups] INT NOT NULL DEFAULT 0,
                            [Remarks] NVARCHAR(255) NULL,
                            [VerifiedByUser] BIT NOT NULL DEFAULT 0,
                            [VerifiedOn] DATETIME2 NULL,
                            CONSTRAINT [FK_TeaEntry_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([UserId]) ON DELETE CASCADE,
                            CONSTRAINT [FK_TeaEntry_MessPeriod_PeriodId] FOREIGN KEY ([PeriodId]) REFERENCES [dbo].[MessPeriod]([PeriodId])
                        );
                        
                        -- Create unique index for one entry per user per date
                        CREATE UNIQUE INDEX [IX_TeaEntry_UserId_Date] ON [dbo].[TeaEntry] ([UserId], [Date]);
                    END
                ");
            }
            catch
            {
                // Table might already exist, continue
            }

            if (!context.Users.Any())
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

            // Sample data for DishPlan and Period if empty
            if (!context.MessPeriods.Any())
            {
                var period = new MessPeriod
                {
                    PeriodName = DateTime.Now.ToString("MMMM yyyy"),
                    StartDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1),
                    EndDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month)),
                    IsActive = true,
                    FixedWaterCharge = 30m,
                    TeaPricePerCup = 0.5m
                };
                context.MessPeriods.Add(period);
                context.SaveChanges();
            }

            if (!context.DishPlans.Any())
            {
                context.DishPlans.AddRange(new[] {
                    // Monday
                    new DishPlan { DayOfWeek = "Monday", MealType = "Breakfast", DishName = "Paratha & Omelette", Price = 80 },
                    new DishPlan { DayOfWeek = "Monday", MealType = "Lunch", DishName = "Chicken Curry", Price = 200 },
                    new DishPlan { DayOfWeek = "Monday", MealType = "Dinner", DishName = "Beef Stew", Price = 220 },
                    // Tuesday
                    new DishPlan { DayOfWeek = "Tuesday", MealType = "Breakfast", DishName = "Halwa Puri", Price = 100 },
                    new DishPlan { DayOfWeek = "Tuesday", MealType = "Lunch", DishName = "Dal Makhni", Price = 120 },
                    new DishPlan { DayOfWeek = "Tuesday", MealType = "Dinner", DishName = "Fish Curry", Price = 240 },
                    // Wednesday
                    new DishPlan { DayOfWeek = "Wednesday", MealType = "Breakfast", DishName = "Eggs & Toast", Price = 60 },
                    new DishPlan { DayOfWeek = "Wednesday", MealType = "Lunch", DishName = "Biryani", Price = 180 },
                    new DishPlan { DayOfWeek = "Wednesday", MealType = "Dinner", DishName = "Mutton Karahi", Price = 280 },
                    // Thursday
                    new DishPlan { DayOfWeek = "Thursday", MealType = "Breakfast", DishName = "Nihari", Price = 150 },
                    new DishPlan { DayOfWeek = "Thursday", MealType = "Lunch", DishName = "Chicken Pulao", Price = 160 },
                    new DishPlan { DayOfWeek = "Thursday", MealType = "Dinner", DishName = "Seekh Kabab", Price = 200 },
                    // Friday
                    new DishPlan { DayOfWeek = "Friday", MealType = "Breakfast", DishName = "Paratha & Chai", Price = 50 },
                    new DishPlan { DayOfWeek = "Friday", MealType = "Lunch", DishName = "Special Biryani", Price = 220 },
                    new DishPlan { DayOfWeek = "Friday", MealType = "Dinner", DishName = "BBQ Platter", Price = 350 },
                    // Saturday
                    new DishPlan { DayOfWeek = "Saturday", MealType = "Breakfast", DishName = "Chana Puri", Price = 90 },
                    new DishPlan { DayOfWeek = "Saturday", MealType = "Lunch", DishName = "Daal Chawal", Price = 100 },
                    new DishPlan { DayOfWeek = "Saturday", MealType = "Dinner", DishName = "Chicken Tikka", Price = 180 },
                    // Sunday
                    new DishPlan { DayOfWeek = "Sunday", MealType = "Breakfast", DishName = "Anda Paratha", Price = 70 },
                    new DishPlan { DayOfWeek = "Sunday", MealType = "Lunch", DishName = "Chicken Handi", Price = 200 },
                    new DishPlan { DayOfWeek = "Sunday", MealType = "Dinner", DishName = "Mix Grill", Price = 300 },
                });
                context.SaveChanges();
            }
        }
    }
}
