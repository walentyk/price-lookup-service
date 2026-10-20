using PriceLookup.Core.Domain;

namespace PriceLookup.Api.Endpoints;

public static class PriceResponseMapper
{
    public static PriceResponse ToResponse(ProductPrice price) =>
        new(price.Sku.Value, price.Price.Amount, price.Price.Currency.Value, price.LastUpdated);
}
