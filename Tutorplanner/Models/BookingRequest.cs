namespace Tutorplanner.Models;

public class BookingRequest
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Contact { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime PreferredStart { get; set; }
    public DateTime? AlternativeStart { get; set; }
    public string Notes { get; set; } = string.Empty;
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public int? StudentId { get; set; }
    public int? LessonId { get; set; }
}