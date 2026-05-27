using System.Globalization;
using OLinq.Exceptions;

namespace OLinq;

internal static class ODataValueFormatter
{
    public static string Format(object? value)
    {
        return value switch
        {
            null => "null",
            bool b => b ? "true" : "false",
            string s => $"'{EscapeString(s)}'",
            char c => $"'{c}'",
            int i => i.ToString(CultureInfo.InvariantCulture),
            long l => $"{l.ToString(CultureInfo.InvariantCulture)}L",
            float f => $"{f.ToString("G", CultureInfo.InvariantCulture)}f",
            double d => d.ToString("G", CultureInfo.InvariantCulture),
            decimal dec => $"{dec.ToString(CultureInfo.InvariantCulture)}M",
            Guid g => $"{g}",
            DateTime dt => dt.Kind == DateTimeKind.Utc
                ? dt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
                : dt.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture),
            DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TimeOnly t => t.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
            TimeSpan ts => $"duration'{FormatDuration(ts)}'",
            byte[] bytes => $"binary'{Convert.ToBase64String(bytes)}'",
            Enum e => $"'{e}'",
            _ when value.GetType().IsEnum => $"'{value}'",
            _ => throw new ODataTranslationException($"Unsupported constant type: {value.GetType().FullName}")
        };
    }

    private static string EscapeString(string s) => s.Replace("'", "''");

    private static string FormatDuration(TimeSpan ts)
    {
        var sb = new System.Text.StringBuilder("P");
        if (ts.Days > 0) sb.Append($"{ts.Days}D");
        if (ts.Hours > 0 || ts.Minutes > 0 || ts.Seconds > 0 || ts.Milliseconds > 0)
        {
            sb.Append('T');
            if (ts.Hours > 0) sb.Append($"{ts.Hours}H");
            if (ts.Minutes > 0) sb.Append($"{ts.Minutes}M");
            if (ts.Seconds > 0 || ts.Milliseconds > 0)
                sb.Append($"{ts.Seconds + ts.Milliseconds / 1000.0}S");
        }
        return sb.ToString();
    }
}
