namespace Tutorplanner.Models
{
    public class RecurringLesson
    {
        public int Id { get; set; }

        public int StudentId { get; set; }
        public Student Student { get; set; } = null!;

        public DayOfWeek DayOfWeek { get; set; }
        public TimeSpan TimeOfDay { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int DurationMinutes { get; set; } = 60;
        public decimal Rate { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
