namespace Tutorplanner.Models;

public sealed record PublicBookingRequest(
    string Name,
    string Contact,
    string Subject,
    DateTime PreferredStart,
    DateTime? AlternativeStart,
    string Notes);

public sealed record PublicAvailabilityResponse(DateTime Date, IReadOnlyList<DateTime> Slots);

public sealed record PublicBookingResponse(int RequestId, string Message);
