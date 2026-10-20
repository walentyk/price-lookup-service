namespace PriceLookup.Api.Endpoints;

public sealed record PriceResponse(string Sku, decimal Price, string Currency, DateTimeOffset LastUpdated);
