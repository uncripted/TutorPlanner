namespace Tutorplanner.Models
{
    public class Lesson
    {
        public int Id { get; set; }

        public int StudentId { get; set; }
        public Student Student { get; set; } = null!;

        public int? RecurringLessonId { get; set; }
        public RecurringLesson? RecurringLesson { get; set; }

        public DateTime Start { get; set; }
        public int DurationMinutes { get; set; } = 60;
        public decimal Rate { get; set; }
        public decimal PaidAmount { get; set; }
        public bool IsCancelled { get; set; }
        public bool IsMissed { get; set; }

        public decimal OutstandingAmount => Math.Max(0, Rate - PaidAmount);
        public bool IsPaid => !IsCancelled && !IsMissed && OutstandingAmount == 0;

        public bool IsCompleted(DateTime now) => !IsCancelled && !IsMissed && Start <= now;
    }
}
