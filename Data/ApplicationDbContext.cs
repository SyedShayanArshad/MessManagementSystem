using Microsoft.EntityFrameworkCore;
using MessManagement.Models;

namespace MessManagement.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<MessPeriod> MessPeriods { get; set; }
        public DbSet<DishPlan> DishPlans { get; set; }
        public DbSet<Attendance> Attendances { get; set; }
        public DbSet<TeaRecord> TeaRecords { get; set; }
        public DbSet<TeaEntry> TeaEntries { get; set; }
        public DbSet<Payment> Payments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Map table and columns to match schema, using explicit table names
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(e => e.UserId);
            });

            modelBuilder.Entity<MessPeriod>(entity =>
            {
                entity.ToTable("MessPeriod");
                entity.HasKey(e => e.PeriodId);
                entity.Property(e => e.FixedWaterCharge).HasPrecision(18, 2);
                entity.Property(e => e.TeaPricePerCup).HasPrecision(18, 2);
            });

            modelBuilder.Entity<DishPlan>(entity =>
            {
                entity.ToTable("DishPlan");
                entity.HasKey(e => e.DishPlanId);
                entity.Property(e => e.Price).HasPrecision(18, 2);
            });

            modelBuilder.Entity<Attendance>(entity =>
 {
     entity.ToTable("Attendance");
     entity.HasKey(e => e.AttendanceId);

     entity.HasOne(a => a.User)
     .WithMany(u => u.Attendances)
     .HasForeignKey(a => a.UserId)
     .OnDelete(DeleteBehavior.NoAction);


     // MAIN DishPlan (keep SetNull)
     entity.HasOne(a => a.DishPlan)
         .WithMany(d => d.Attendances)
         .HasForeignKey(a => a.DishPlanId)
         .OnDelete(DeleteBehavior.SetNull);

     // Meal-wise DishPlans (NO cascade)
     entity.HasOne(a => a.BreakfastDishPlan)
         .WithMany()
         .HasForeignKey(a => a.BreakfastDishPlanId)
         .OnDelete(DeleteBehavior.NoAction);

     entity.HasOne(a => a.LunchDishPlan)
         .WithMany()
         .HasForeignKey(a => a.LunchDishPlanId)
         .OnDelete(DeleteBehavior.NoAction);

     entity.HasOne(a => a.DinnerDishPlan)
         .WithMany()
         .HasForeignKey(a => a.DinnerDishPlanId)
         .OnDelete(DeleteBehavior.NoAction);

     entity.HasOne(a => a.MessPeriod)
         .WithMany(p => p.Attendances)
         .HasForeignKey(a => a.PeriodId)
         .OnDelete(DeleteBehavior.NoAction);
 });


            modelBuilder.Entity<TeaRecord>(entity =>
            {
                entity.ToTable("TeaRecord");
                entity.HasKey(e => e.TeaRecordId);
                entity.HasOne(t => t.MessPeriod)
                    .WithMany(p => p.TeaRecords)
                    .HasForeignKey(t => t.PeriodId);
            });

            modelBuilder.Entity<TeaEntry>(entity =>
            {
                entity.ToTable("TeaEntry");
                entity.HasKey(e => e.TeaEntryId);
                entity.HasOne(t => t.User)
                    .WithMany(u => u.TeaEntries)
                    .HasForeignKey(t => t.UserId)
                    .OnDelete(DeleteBehavior.NoAction);
                entity.HasOne(t => t.MessPeriod)
                    .WithMany(p => p.TeaEntries)
                    .HasForeignKey(t => t.PeriodId);
                // Unique constraint: one entry per user per date
                entity.HasIndex(t => new { t.UserId, t.Date }).IsUnique();
            });

            modelBuilder.Entity<Payment>(entity =>
{
    entity.ToTable("Payment");
    entity.HasKey(e => e.PaymentId);

    entity.Property(e => e.Amount).HasPrecision(18, 2);

    // MAIN relationship (keep cascade)
    entity.HasOne(p => p.User)
        .WithMany(u => u.Payments)
        .HasForeignKey(p => p.UserId)
        .OnDelete(DeleteBehavior.NoAction);

    // Approver user (NO cascade)
    entity.HasOne(p => p.ApprovedByUser)
        .WithMany()
        .HasForeignKey(p => p.ApprovedByUserId)
        .OnDelete(DeleteBehavior.NoAction);

    entity.HasOne(p => p.MessPeriod)
        .WithMany(period => period.Payments)
        .HasForeignKey(p => p.PeriodId)
        .OnDelete(DeleteBehavior.NoAction);

    entity.HasOne(p => p.Attendance)
        .WithMany(a => a.Payments)
        .HasForeignKey(p => p.AttendanceId)
        .OnDelete(DeleteBehavior.SetNull);
});


            base.OnModelCreating(modelBuilder);
        }
    }
}
