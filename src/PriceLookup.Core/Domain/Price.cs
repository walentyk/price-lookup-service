using System.Diagnostics.CodeAnalysis;

namespace PriceLookup.Core.Domain;

public sealed record Price
{
    private Price(decimal amount, CurrencyCode currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public CurrencyCode Currency { get; }

    public static bool TryCreate(decimal amount, CurrencyCode currency, [NotNullWhen(true)] out Price? price)
    {
        price = amount >= 0 ? new Price(amount, currency) : null;
        return price is not null;
    }
}
