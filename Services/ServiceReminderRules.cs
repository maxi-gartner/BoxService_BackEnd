using System.Globalization;
using System.Text.RegularExpressions;

namespace BoxService_BackEnd.Services;

public static class ServiceReminderRules
{
    public const int AdvanceDays = 7;

    public static DateOnly Today(DateTimeOffset? now = null) => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTimeBySystemTimeZoneId(now ?? DateTimeOffset.UtcNow,
            "America/Argentina/Buenos_Aires").DateTime);

    public static string Status(DateOnly due, DateOnly today) =>
        due < today ? "overdue" : due == today ? "today" : "upcoming";

    public static bool IsDue(DateOnly due, DateOnly today) =>
        due.DayNumber - today.DayNumber <= AdvanceDays;

    // El código de país debe estar cargado; no adivinarlo a partir del área local.
    public static string? PhoneDigits(string? phone)
    {
        var raw = phone?.Trim() ?? "";
        var digits = Regex.Replace(raw, @"\D", "");
        if (raw.StartsWith("00")) digits = digits[2..];
        var minimum = raw.StartsWith('+') || raw.StartsWith("00") ? 8 : 11;
        return digits.Length >= minimum && digits.Length <= 15 && digits[0] != '0'
            ? digits : null;
    }

    public static string? WhatsAppUrl(string? phone, string client, string workshop,
        string vehicle, DateOnly due)
    {
        var digits = PhoneDigits(phone);
        if (digits is null) return null;
        var date = due.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var message = $"Hola {client}, te escribimos de {workshop}. Según nuestro registro, " +
            $"el próximo service de tu {vehicle} corresponde para el {date}. ¿Querés coordinar un turno?";
        return $"https://wa.me/{digits}?text={Uri.EscapeDataString(message)}";
    }
}
