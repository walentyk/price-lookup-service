using System.Globalization;
using System.Text.RegularExpressions;

namespace PriceLookup.Core.Infrastructure.Sap.OData;

public static partial class ODataDateParser
{
    private static readonly long MinMilliseconds = DateTimeOffset.MinValue.ToUnixTimeMilliseconds();
    private static readonly long MaxMilliseconds = DateTimeOffset.MaxValue.ToUnixTimeMilliseconds();

    // A9: "/Date(ms)/" or "/Date(ms+hhmm)/". The milliseconds are UTC, so the offset is ignored.
    public static bool TryParse(string? value, out DateTimeOffset result)
    {
        result = default;
        var match = value is null ? Match.Empty : Pattern().Match(value);
        if (!match.Success || !TryParseMilliseconds(match.Groups["ms"].Value, out var milliseconds))
        {
            return false;
        }

        result = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
        return true;
    }

    private static bool TryParseMilliseconds(string digits, out long milliseconds) =>
        long.TryParse(digits, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out milliseconds)
        && milliseconds >= MinMilliseconds
        && milliseconds <= MaxMilliseconds;

    [GeneratedRegex(@"^/Date\((?<ms>-?[0-9]{1,19})([+-][0-9]{4})?\)/$")]
    private static partial Regex Pattern();
}
