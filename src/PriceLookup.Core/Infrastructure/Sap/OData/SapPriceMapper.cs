using System.Globalization;
using PriceLookup.Core.Application.Ports;
using PriceLookup.Core.Domain;

namespace PriceLookup.Core.Infrastructure.Sap.OData;

public static class SapPriceMapper
{
    // Edm.Decimal: optional sign, digits, optional decimal point. No thousands separators, no exponent.
    private const NumberStyles DecimalStyle = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    // The requested SKU is the identity of the result; SAP's echo of it is not trusted.
    public static ProductPrice ToProductPrice(SapProductPriceDto dto, Sku requestedSku) =>
        new(requestedSku, ToPrice(dto), ToLastUpdated(dto.LastChangedAt));

    private static Price ToPrice(SapProductPriceDto dto) =>
        Price.TryCreate(ToAmount(dto.Price), ToCurrency(dto.Currency), out var price)
            ? price
            : throw Invalid($"SAP returned a negative price '{dto.Price}'.");

    private static decimal ToAmount(string? value) =>
        decimal.TryParse(value, DecimalStyle, CultureInfo.InvariantCulture, out var amount)
            ? amount
            : throw Invalid($"SAP returned an unparsable price '{value}'.");

    private static CurrencyCode ToCurrency(string? value) =>
        CurrencyCode.TryParse(value, out var currency)
            ? currency
            : throw Invalid($"SAP returned an invalid currency '{value}'.");

    private static DateTimeOffset ToLastUpdated(string? value) =>
        ODataDateParser.TryParse(value, out var lastUpdated)
            ? lastUpdated
            : throw Invalid($"SAP returned an unparsable LastChangedAt '{value}'.");

    private static ErpException Invalid(string message) => new(ErpFailureKind.InvalidResponse, message);
}
