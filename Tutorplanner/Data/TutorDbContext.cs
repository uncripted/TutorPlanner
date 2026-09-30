using Microsoft.EntityFrameworkCore;
using Tutorplanner.Models;

namespace Tutorplanner.Data
{
    public class TutorDbContext : DbContext
    {
        public TutorDbContext(DbContextOptions<TutorDbContext> options) : base(options)
        {
        }

        public DbSet<Student> Students { get; set; } = null!;
        public DbSet<RecurringLesson> RecurringLessons { get; set; } = null!;
        public DbSet<Lesson> Lessons { get; set; } = null!;
        public DbSet<Payment> Payments { get; set; } = null!;
        public DbSet<BookingRequest> BookingRequests { get; set; } = null!;
        public DbSet<WorkingHour> WorkingHours { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Student>().Property(s => s.Name).HasMaxLength(200).IsRequired();
            modelBuilder.Entity<Student>().HasIndex(s => s.Name);

            modelBuilder.Entity<Lesson>().Property(l => l.Rate).HasPrecision(18, 2);
            modelBuilder.Entity<Lesson>().Property(l => l.PaidAmount).HasPrecision(18, 2);
            modelBuilder.Entity<RecurringLesson>().Property(l => l.Rate).HasPrecision(18, 2);
            modelBuilder.Entity<Payment>().Property(p => p.Amount).HasPrecision(18, 2);

            modelBuilder.Entity<Student>().HasMany(s => s.RecurringLessons)
                .WithOne(r => r.Student).HasForeignKey(r => r.StudentId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Student>().HasMany(s => s.Lessons)
                .WithOne(l => l.Student).HasForeignKey(l => l.StudentId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Student>().HasMany(s => s.Payments)
                .WithOne(p => p.Student).HasForeignKey(p => p.StudentId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<RecurringLesson>().HasMany<Lesson>()
                .WithOne(l => l.RecurringLesson).HasForeignKey(l => l.RecurringLessonId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<Lesson>().HasIndex(l => new { l.StudentId, l.Start });
            modelBuilder.Entity<Lesson>().HasIndex(l => new { l.RecurringLessonId, l.Start }).IsUnique();
            modelBuilder.Entity<BookingRequest>().Property(b => b.Name).HasMaxLength(200).IsRequired();
            modelBuilder.Entity<BookingRequest>().Property(b => b.Contact).HasMaxLength(300).IsRequired();
            modelBuilder.Entity<WorkingHour>().HasIndex(w => w.Day).IsUnique();
        }
    }
}
