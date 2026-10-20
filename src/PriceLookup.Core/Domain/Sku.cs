using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace PriceLookup.Core.Domain;

public sealed partial record Sku
{
    private Sku(string value) => Value = value;

    public string Value { get; }

    public static bool TryParse(string? raw, [NotNullWhen(true)] out Sku? sku)
    {
        var normalised = raw?.Trim().ToUpperInvariant();
        sku = normalised is not null && Pattern().IsMatch(normalised) ? new Sku(normalised) : null;
        return sku is not null;
    }

    public override string ToString() => Value;

    // A11: SAP material number, up to 40 characters, no quotes so the OData key literal cannot break.
    [GeneratedRegex("^[A-Z0-9][A-Z0-9._-]{0,39}$")]
    private static partial Regex Pattern();
}
