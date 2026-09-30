namespace Tutorplanner.Models;

public class WorkingHour
{
    public int Id { get; set; }
    public DayOfWeek Day { get; set; }
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }
    public bool IsEnabled { get; set; } = true;
}