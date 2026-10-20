namespace PriceLookup.Core.Infrastructure.Sap.OData;

// Everything is a string: Edm.Decimal and Edm.DateTimeOffset are strings in OData V2 JSON,
// and nothing coming from SAP is trusted before SapPriceMapper has parsed it.
public sealed record SapProductPriceDto(string? Sku, string? Price, string? Currency, string? LastChangedAt);
