using System.Diagnostics.CodeAnalysis;
using PriceLookup.Core.Domain;

namespace PriceLookup.Core.Application.GetProductPrice;

public sealed record PriceLookupResult
{
    private PriceLookupResult(ProductPrice? price, PriceLookupError? error)
    {
        Price = price;
        Error = error;
    }

    public ProductPrice? Price { get; }

    public PriceLookupError? Error { get; }

    [MemberNotNullWhen(true, nameof(Price))]
    public bool IsSuccess => Price is not null;

    public static PriceLookupResult Found(ProductPrice price) => new(price, null);

    public static PriceLookupResult Failed(PriceLookupError error) => new(null, error);
}
