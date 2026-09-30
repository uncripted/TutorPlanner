namespace Tutorplanner.Services;

public static class CurrencyFormatter
{
    public static string Gel(decimal amount) => $"{amount:N2} GEL";
}