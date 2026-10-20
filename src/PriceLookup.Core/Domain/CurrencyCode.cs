using System.Diagnostics.CodeAnalysis;

namespace PriceLookup.Core.Domain;

public sealed record CurrencyCode
{
    private CurrencyCode(string value) => Value = value;

    public string Value { get; }

    public static bool TryParse(string? raw, [NotNullWhen(true)] out CurrencyCode? code)
    {
        var normalised = raw?.Trim().ToUpperInvariant();
        code = IsThreeLetters(normalised) ? new CurrencyCode(normalised) : null;
        return code is not null;
    }

    public override string ToString() => Value;

    private static bool IsThreeLetters([NotNullWhen(true)] string? value) =>
        value is { Length: 3 } && value.All(char.IsAsciiLetterUpper);
}
