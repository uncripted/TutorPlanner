using Microsoft.EntityFrameworkCore;
using Tutorplanner.Models;

namespace Tutorplanner.Data;

public static class TutorPlannerDatabase
{
    public static async Task InitializeAsync(TutorDbContext db)
    {
        db.Database.EnsureCreated();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "BookingRequests" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_BookingRequests" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL,
                "Contact" TEXT NOT NULL,
                "Subject" TEXT NOT NULL,
                "PreferredStart" TEXT NOT NULL,
                "AlternativeStart" TEXT NULL,
                "Notes" TEXT NOT NULL,
                "Status" INTEGER NOT NULL,
                "SubmittedAt" TEXT NOT NULL,
                "StudentId" INTEGER NULL,
                "LessonId" INTEGER NULL
            );
            CREATE TABLE IF NOT EXISTS "WorkingHours" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_WorkingHours" PRIMARY KEY AUTOINCREMENT,
                "Day" INTEGER NOT NULL,
                "Start" TEXT NOT NULL,
                "End" TEXT NOT NULL,
                "IsEnabled" INTEGER NOT NULL
            );
            """);

        if (!await db.WorkingHours.AnyAsync())
        {
            foreach (var day in Enum.GetValues<DayOfWeek>().Where(d => d is not DayOfWeek.Saturday and not DayOfWeek.Sunday))
            {
                db.WorkingHours.Add(new WorkingHour { Day = day, Start = new TimeSpan(9, 0, 0), End = new TimeSpan(17, 0, 0) });
            }
            await db.SaveChangesAsync();
        }
    }
}