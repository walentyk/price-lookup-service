using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace PriceLookup.SapStub;

// Bodies shaped like SAP Gateway OData V2 JSON (A9).
public static class ODataPayloads
{
    private const string LastChangedAt = "/Date(1760868000000+0000)/";

    // Default options keep the SAP property names as written; the relaxed encoder keeps '+' in /Date(...)/ readable.
    private static readonly JsonSerializerOptions Options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static IResult Price(string sku, decimal amount, string currency) =>
        Results.Json(new { d = PriceEntity(sku, amount, currency) }, Options);

    public static IResult NotFound() =>
        Error(StatusCodes.Status404NotFound, "/IWBEP/CM_MGW_RT/020", "Resource not found for segment 'ProductPrice'");

    public static IResult BadRequest(string segment) =>
        Error(StatusCodes.Status400BadRequest, "/IWBEP/CM_MGW_RT/022", $"Invalid key predicate in segment '{segment}'");

    public static IResult Error(int statusCode, string code, string message) =>
        Results.Json(new { error = new { code, message = new { lang = "en", value = message } } }, Options, statusCode: statusCode);

    // Cut off in the middle of the object, so it is not valid JSON.
    public static IResult Malformed(string sku) =>
        Results.Text($$"""{ "d": { "Sku": "{{sku}}", "Price": """, "application/json");

    private static object PriceEntity(string sku, decimal amount, string currency) => new
    {
        __metadata = new { type = "ZSD_PRODUCT_PRICE_SRV.ProductPrice" },
        Sku = sku,
        Price = amount.ToString("0.00", CultureInfo.InvariantCulture),
        Currency = currency,
        LastChangedAt,
    };
}
