namespace PriceLookup.Core.Domain;

public sealed record ProductPrice(Sku Sku, Price Price, DateTimeOffset LastUpdated);
