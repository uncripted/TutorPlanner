using Microsoft.EntityFrameworkCore;
using Tutorplanner.Data;
using Tutorplanner.Models;

namespace Tutorplanner.Services;

public sealed class TutorPlannerService
{
    private readonly TutorDbContext _db;

    public TutorPlannerService(TutorDbContext db)
    {
        _db = db;
    }

    public async Task GenerateLessonsAsync(DateTime throughDate, CancellationToken cancellationToken = default)
    {
        var rules = await _db.RecurringLessons
            .Where(r => r.IsActive && r.StartDate.Date <= throughDate.Date)
            .ToListAsync(cancellationToken);

        foreach (var rule in rules)
        {
            var first = NextOccurrence(rule.StartDate.Date, rule.DayOfWeek);
            var last = rule.EndDate?.Date < throughDate.Date ? rule.EndDate.Value.Date : throughDate.Date;
            var existing = await _db.Lessons
                .Where(l => l.RecurringLessonId == rule.Id)
                .Select(l => l.Start)
                .ToHashSetAsync(cancellationToken);

            for (var date = first; date <= last; date = date.AddDays(7))
            {
                var start = date.Add(rule.TimeOfDay);
                if (existing.Contains(start))
                {
                    continue;
                }

                _db.Lessons.Add(new Lesson
                {
                    StudentId = rule.StudentId,
                    RecurringLessonId = rule.Id,
                    Start = start,
                    DurationMinutes = rule.DurationMinutes,
                    Rate = rule.Rate
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PaymentResult> AddPaymentAsync(int studentId, decimal amount, DateTime date, string notes = "")
    {
        if (amount <= 0)
        {
            return new PaymentResult(false, "Enter a payment amount greater than zero.", 0);
        }

        var payment = new Payment { StudentId = studentId, Amount = amount, Date = date.Date, Notes = notes.Trim() };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();
        await EnsurePaymentCoverageAsync(studentId);
        await ReallocateAllPaymentsAsync();

        var allocated = await _db.Lessons
            .Where(l => l.StudentId == studentId && !l.IsCancelled && !l.IsMissed)
            .SumAsync(l => (decimal?)l.PaidAmount) ?? 0;
        var totalPayments = await _db.Payments
            .Where(p => p.StudentId == studentId)
            .SumAsync(p => (decimal?)p.Amount) ?? 0;
        var remaining = Math.Max(0, totalPayments - allocated);
        return new PaymentResult(true, "Payment recorded.", remaining);
    }

    public async Task<PaymentResult> MarkLessonPaidAsync(int lessonId)
    {
        var lesson = await _db.Lessons.FirstOrDefaultAsync(l => l.Id == lessonId);
        if (lesson is null)
        {
            return new PaymentResult(false, "Lesson not found.", 0);
        }

        if (lesson.IsCancelled || lesson.IsMissed)
        {
            return new PaymentResult(false, "Cancelled and missed lessons cannot be marked paid.", 0);
        }

        var remaining = Math.Max(0, lesson.Rate - lesson.PaidAmount);
        if (remaining == 0)
        {
            return new PaymentResult(false, "This lesson is already paid.", 0);
        }

        _db.Payments.Add(new Payment
        {
            StudentId = lesson.StudentId,
            Amount = remaining,
            Date = DateTime.Today,
            Notes = $"Lesson marked paid ({lesson.Start:yyyy-MM-dd HH:mm})"
        });
        lesson.PaidAmount = lesson.Rate;
        await _db.SaveChangesAsync();
        return new PaymentResult(true, "Lesson marked paid and payment recorded.", 0);
    }

    public async Task ReallocateAllPaymentsAsync(CancellationToken cancellationToken = default)
    {
        var paymentTotals = await _db.Payments
            .GroupBy(p => p.StudentId)
            .Select(g => new { StudentId = g.Key, Total = g.Sum(p => p.Amount) })
            .ToListAsync(cancellationToken);

        foreach (var total in paymentTotals)
        {
            await EnsurePaymentCoverageAsync(total.StudentId, cancellationToken);
        }

        var allLessons = await _db.Lessons
            .OrderBy(l => l.StudentId)
            .ThenBy(l => l.Start)
            .ThenBy(l => l.Id)
            .ToListAsync(cancellationToken);
        var lessons = allLessons.Where(l => !l.IsCancelled && !l.IsMissed).ToList();
        var payments = await _db.Payments
            .OrderBy(p => p.StudentId)
            .ThenBy(p => p.Date)
            .ThenBy(p => p.Id)
            .ToListAsync(cancellationToken);

        foreach (var lesson in allLessons)
        {
            lesson.PaidAmount = 0;
        }

        foreach (var payment in payments)
        {
            var remaining = payment.Amount;
            foreach (var lesson in lessons.Where(l => l.StudentId == payment.StudentId && l.PaidAmount < l.Rate))
            {
                if (remaining <= 0) break;
                var due = lesson.Rate - lesson.PaidAmount;
                var applied = Math.Min(remaining, due);
                lesson.PaidAmount += applied;
                remaining -= applied;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsurePaymentCoverageAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var totalPayments = await _db.Payments
            .Where(p => p.StudentId == studentId)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0;

        await EnsurePaymentCoverageAsync(studentId, totalPayments, cancellationToken);
    }

    private async Task EnsurePaymentCoverageAsync(int studentId, decimal totalPayments, CancellationToken cancellationToken = default)
    {
        if (totalPayments <= 0)
        {
            return;
        }

        var throughDate = DateTime.Today.AddDays(30);
        const int maximumYearsToMaterialize = 10;

        for (var year = 0; year <= maximumYearsToMaterialize; year++)
        {
            var eligibleValue = await _db.Lessons
                .Where(l => l.StudentId == studentId && !l.IsCancelled && !l.IsMissed)
                .SumAsync(l => (decimal?)l.Rate, cancellationToken) ?? 0;

            if (eligibleValue >= totalPayments)
            {
                return;
            }

            var beforeCount = await _db.Lessons.CountAsync(l => l.StudentId == studentId, cancellationToken);
            await GenerateLessonsAsync(throughDate, cancellationToken);
            var afterCount = await _db.Lessons.CountAsync(l => l.StudentId == studentId, cancellationToken);

            if (afterCount == beforeCount)
            {
                return;
            }

            throughDate = throughDate.AddYears(1);
        }
    }

    public async Task<StudentBalance> GetBalanceAsync(int studentId)
    {
        var lessons = await _db.Lessons
            .Where(l => l.StudentId == studentId && !l.IsCancelled && !l.IsMissed && l.Start <= DateTime.Now)
            .ToListAsync();
        var payments = await _db.Payments.Where(p => p.StudentId == studentId).SumAsync(p => (decimal?)p.Amount) ?? 0;
        var charged = lessons.Sum(l => l.Rate);
        var allocated = lessons.Sum(l => l.PaidAmount);
        return new StudentBalance(charged, allocated, Math.Max(0, charged - allocated), payments, lessons.Count(l => l.Rate > l.PaidAmount));
    }

    public async Task<decimal> GetEstimatedIncomeAsync(DateTime month)
    {
        var start = new DateTime(month.Year, month.Month, 1);
        var end = start.AddMonths(1);
        return await _db.Lessons
            .Where(l => l.Start >= start && l.Start < end && !l.IsCancelled && !l.IsMissed)
            .SumAsync(l => (decimal?)l.Rate) ?? 0;
    }

    public async Task SetMissedAsync(int lessonId, bool missed)
    {
        var lesson = await _db.Lessons.FindAsync(lessonId);
        if (lesson is null || lesson.Start > DateTime.Now || lesson.IsCancelled)
        {
            return;
        }

        lesson.IsMissed = missed;
        await _db.SaveChangesAsync();
    }

    public Task<List<Payment>> GetPaymentHistoryAsync(int studentId) =>
        _db.Payments.Where(p => p.StudentId == studentId).OrderByDescending(p => p.Date).ThenByDescending(p => p.Id).ToListAsync();

    public async Task<List<DateTime>> GetAvailableSlotsAsync(DateTime date, int durationMinutes = 60)
    {
        var hours = await _db.WorkingHours.FirstOrDefaultAsync(w => w.Day == date.DayOfWeek && w.IsEnabled);
        if (hours is null) return new();
        var lessons = await _db.Lessons.Where(l => l.Start.Date == date.Date && !l.IsCancelled && !l.IsMissed).ToListAsync();
        var slots = new List<DateTime>();
        for (var slot = date.Date.Add(hours.Start); slot.AddMinutes(durationMinutes) <= date.Date.Add(hours.End); slot = slot.AddMinutes(30))
        {
            var slotEnd = slot.AddMinutes(durationMinutes);
            if (!lessons.Any(l => l.Start < slotEnd && l.Start.AddMinutes(l.DurationMinutes) > slot)) slots.Add(slot);
        }
        return slots;
    }

    public async Task<int> SubmitBookingAsync(BookingRequest request)
    {
        request.Name = request.Name.Trim();
        request.Contact = request.Contact.Trim();
        request.Subject = request.Subject.Trim();
        request.Notes = request.Notes.Trim();
        request.Status = BookingStatus.Pending;
        request.SubmittedAt = DateTime.UtcNow;
        _db.BookingRequests.Add(request);
        await _db.SaveChangesAsync();
        return request.Id;
    }

    public async Task ApproveBookingAsync(int requestId, int studentId, DateTime start, int durationMinutes, decimal rate)
    {
        var request = await _db.BookingRequests.FindAsync(requestId);
        if (request is null || request.Status != BookingStatus.Pending) return;
        var lesson = new Lesson { StudentId = studentId, Start = start, DurationMinutes = durationMinutes, Rate = rate };
        _db.Lessons.Add(lesson);
        await _db.SaveChangesAsync();
        request.StudentId = studentId;
        request.LessonId = lesson.Id;
        request.Status = BookingStatus.Approved;
        await _db.SaveChangesAsync();
    }

    public async Task RejectBookingAsync(int requestId)
    {
        var request = await _db.BookingRequests.FindAsync(requestId);
        if (request is null) return;
        request.Status = BookingStatus.Rejected;
        await _db.SaveChangesAsync();
    }

    public async Task EditRecurringScheduleAsync(
        int recurringLessonId,
        ScheduleEditScope scope,
        DateTime effectiveDate,
        DayOfWeek day,
        TimeSpan time,
        int durationMinutes,
        decimal rate,
        DateTime? endDate,
        DateTime? occurrenceDate = null)
    {
        var rule = await _db.RecurringLessons.Include(r => r.Student).FirstOrDefaultAsync(r => r.Id == recurringLessonId);
        if (rule is null) return;

        if (scope == ScheduleEditScope.OneOccurrence)
        {
            if (occurrenceDate is null) return;
            var occurrence = await _db.Lessons.FirstOrDefaultAsync(l => l.RecurringLessonId == recurringLessonId && l.Start.Date == occurrenceDate.Value.Date);
            if (occurrence is null) return;

            occurrence.RecurringLessonId = null;
            occurrence.Start = occurrenceDate.Value.Date + time;
            occurrence.DurationMinutes = durationMinutes;
            occurrence.Rate = rate;
            await _db.SaveChangesAsync();
            return;
        }

        if (scope == ScheduleEditScope.FromThisDate)
        {
            var newRule = new RecurringLesson
            {
                StudentId = rule.StudentId,
                DayOfWeek = day,
                TimeOfDay = time,
                StartDate = effectiveDate.Date,
                EndDate = endDate?.Date,
                DurationMinutes = durationMinutes,
                Rate = rate,
                IsActive = true
            };
            rule.EndDate = effectiveDate.Date.AddDays(-1);
            rule.IsActive = false;

            var futureLessons = await _db.Lessons
                .Where(l => l.RecurringLessonId == rule.Id && l.Start.Date >= effectiveDate.Date && !l.IsCancelled)
                .ToListAsync();
            foreach (var lesson in futureLessons)
            {
                lesson.IsCancelled = true;
            }

            _db.RecurringLessons.Add(newRule);
            await _db.SaveChangesAsync();
            await GenerateLessonsAsync(DateTime.Today.AddMonths(12));
            return;
        }

        var oldFirst = NextOccurrence(rule.StartDate.Date, rule.DayOfWeek);
        var allLessons = await _db.Lessons
            .Where(l => l.RecurringLessonId == rule.Id)
            .OrderBy(l => l.Start)
            .ToListAsync();

        rule.DayOfWeek = day;
        rule.TimeOfDay = time;
        rule.StartDate = effectiveDate.Date;
        rule.EndDate = endDate?.Date;
        rule.DurationMinutes = durationMinutes;
        rule.Rate = rate;
        rule.IsActive = true;

        var newFirst = NextOccurrence(rule.StartDate.Date, rule.DayOfWeek);
        foreach (var lesson in allLessons)
        {
            var week = Math.Max(0, (int)Math.Floor((lesson.Start.Date - oldFirst.Date).TotalDays / 7));
            lesson.Start = newFirst.AddDays(week * 7).Add(rule.TimeOfDay);
            lesson.DurationMinutes = durationMinutes;
            lesson.Rate = rate;
        }

        await _db.SaveChangesAsync();
        await GenerateLessonsAsync(DateTime.Today.AddMonths(12));
    }

    public async Task DeleteStudentAsync(int studentId)
    {
        var student = await _db.Students.FindAsync(studentId);
        if (student is null) return;

        _db.Students.Remove(student);
        await _db.SaveChangesAsync();
    }

    private static DateTime NextOccurrence(DateTime date, DayOfWeek day)
    {
        var offset = ((int)day - (int)date.DayOfWeek + 7) % 7;
        return date.AddDays(offset);
    }
}

public sealed record PaymentResult(bool Success, string Message, decimal UnallocatedAmount);

public sealed record StudentBalance(
    decimal Charged,
    decimal Allocated,
    decimal Outstanding,
    decimal TotalPayments,
    int UnpaidLessonCount);