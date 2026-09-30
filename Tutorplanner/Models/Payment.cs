namespace Tutorplanner.Models
{
    public class Payment
    {
        public int Id { get; set; }

        public int StudentId { get; set; }
        public Student Student { get; set; } = null!;

        public DateTime Date { get; set; } = DateTime.Today;
        public decimal Amount { get; set; }
        public string Notes { get; set; } = string.Empty;
    }
}
