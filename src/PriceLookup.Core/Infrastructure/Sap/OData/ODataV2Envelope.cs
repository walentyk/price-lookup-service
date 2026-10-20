using System.Text.Json.Serialization;

namespace PriceLookup.Core.Infrastructure.Sap.OData;

// OData V2 JSON wraps a single entity in "d".
public sealed record ODataV2Envelope([property: JsonPropertyName("d")] SapProductPriceDto? D);
