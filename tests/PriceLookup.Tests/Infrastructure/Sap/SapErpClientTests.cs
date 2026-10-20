using System.Net;
using PriceLookup.Core.Application.Ports;
using PriceLookup.Core.Domain;
using PriceLookup.Core.Infrastructure.Sap;
using PriceLookup.Tests.Fakes;

namespace PriceLookup.Tests.Infrastructure.Sap;

public sealed class SapErpClientTests
{
    // The A9 success payload.
    private const string FoundPayload = """
        {
          "d": {
            "__metadata": { "type": "ZSD_PRODUCT_PRICE_SRV.ProductPrice" },
            "Sku": "ABC-123",
            "Price": "19.99",
            "Currency": "EUR",
            "LastChangedAt": "/Date(1760868000000+0000)/"
          }
        }
        """;

    // The A9 SAP Gateway 404 body.
    private const string NotFoundPayload = """
        { "error": { "code": "/IWBEP/CM_MGW_RT/020", "message": { "lang": "en", "value": "Resource not found for segment 'ProductPrice'" } } }
        """;

    private static readonly DateTimeOffset LastChangedAt = new(2025, 10, 19, 10, 0, 0, TimeSpan.Zero);
    private static readonly Sku AbcSku = Sku.TryParse("ABC-123", out var sku) ? sku : throw new InvalidOperationException();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Status_200_with_the_contract_payload_returns_the_price()
    {
        var price = await GetPriceAsync(StubHttpMessageHandler.Returning(HttpStatusCode.OK, FoundPayload));

        Assert.NotNull(price);
        Assert.Equal("ABC-123", price.Sku.Value);
        Assert.Equal(19.99m, price.Price.Amount);
        Assert.Equal("EUR", price.Price.Currency.Value);
        Assert.Equal(LastChangedAt, price.LastUpdated);
    }

    [Fact]
    public async Task Date_without_offset_also_parses()
    {
        var payload = Payload(lastChangedAt: "/Date(1760868000000)/");

        var price = await GetPriceAsync(StubHttpMessageHandler.Returning(HttpStatusCode.OK, payload));

        Assert.NotNull(price);
        Assert.Equal(LastChangedAt, price.LastUpdated);
    }

    [Fact]
    public async Task Result_carries_the_requested_sku_not_the_one_sap_echoes()
    {
        var payload = Payload(sku: "SOMETHING-ELSE");

        var price = await GetPriceAsync(StubHttpMessageHandler.Returning(HttpStatusCode.OK, payload));

        Assert.NotNull(price);
        Assert.Equal("ABC-123", price.Sku.Value);
    }

    [Fact]
    public async Task Status_404_returns_null()
    {
        var price = await GetPriceAsync(StubHttpMessageHandler.Returning(HttpStatusCode.NotFound, NotFoundPayload));

        Assert.Null(price);
    }

    [Theory]
    [InlineData(401, ErpFailureKind.Unauthorized)]
    [InlineData(403, ErpFailureKind.Unauthorized)]
    [InlineData(408, ErpFailureKind.Transient)]
    [InlineData(429, ErpFailureKind.Transient)]
    [InlineData(500, ErpFailureKind.Transient)]
    [InlineData(503, ErpFailureKind.Transient)]
    [InlineData(400, ErpFailureKind.InvalidResponse)]
    public async Task Error_status_throws_the_matching_failure_after_one_call(int status, ErpFailureKind expected)
    {
        var handler = StubHttpMessageHandler.Returning((HttpStatusCode)status);

        var exception = await Assert.ThrowsAsync<ErpException>(() => GetPriceAsync(handler));

        Assert.Equal(expected, exception.Kind);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("""{ "value": [] }""")]
    [InlineData("""{ "d": { "Price": 19.99, "Currency": "EUR", "LastChangedAt": "/Date(1760868000000)/" } }""")]
    public async Task Malformed_body_or_missing_d_throws_invalid_response(string body)
    {
        var exception = await Assert.ThrowsAsync<ErpException>(
            () => GetPriceAsync(StubHttpMessageHandler.Returning(HttpStatusCode.OK, body)));

        Assert.Equal(ErpFailureKind.InvalidResponse, exception.Kind);
    }

    [Theory]
    [InlineData("abc", "EUR", "/Date(1760868000000)/")]
    [InlineData("19,99", "EUR", "/Date(1760868000000)/")]
    [InlineData("-1.00", "EUR", "/Date(1760868000000)/")]
    [InlineData(null, "EUR", "/Date(1760868000000)/")]
    [InlineData("19.99", "EURO", "/Date(1760868000000)/")]
    [InlineData("19.99", null, "/Date(1760868000000)/")]
    [InlineData("19.99", "EUR", "2025-10-19T10:00:00Z")]
    [InlineData("19.99", "EUR", null)]
    public async Task Unparsable_price_currency_or_date_throws_invalid_response(string? amount, string? currency, string? date)
    {
        var payload = Payload(amount: amount, currency: currency, lastChangedAt: date);

        var exception = await Assert.ThrowsAsync<ErpException>(
            () => GetPriceAsync(StubHttpMessageHandler.Returning(HttpStatusCode.OK, payload)));

        Assert.Equal(ErpFailureKind.InvalidResponse, exception.Kind);
    }

    [Fact]
    public async Task Network_error_throws_transient_after_one_call()
    {
        var handler = StubHttpMessageHandler.Throwing(new HttpRequestException("Connection refused"));

        var exception = await Assert.ThrowsAsync<ErpException>(() => GetPriceAsync(handler));

        Assert.Equal(ErpFailureKind.Transient, exception.Kind);
        Assert.IsType<HttpRequestException>(exception.InnerException);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Cancellation_passes_through_unwrapped()
    {
        var handler = StubHttpMessageHandler.Throwing(new TaskCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => GetPriceAsync(handler));
    }

    [Fact]
    public async Task Request_targets_the_product_price_entity_with_client_and_json_format()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, FoundPayload);

        await GetPriceAsync(handler);

        var url = Assert.Single(handler.Requests).RequestUri?.ToString();
        Assert.NotNull(url);
        Assert.StartsWith("https://sap.test/", url);
        Assert.Contains("ProductPriceSet('ABC-123')", url);
        Assert.Contains("sap-client=100", url);
        Assert.Contains("$format=json", url);
    }

    private static async Task<ProductPrice?> GetPriceAsync(StubHttpMessageHandler handler)
    {
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://sap.test") };
        var client = new SapErpClient(httpClient, new SapRequestFactory(new SapOptions()));
        return await client.GetPriceAsync(AbcSku, Ct);
    }

    private static string Payload(
        string? sku = "ABC-123",
        string? amount = "19.99",
        string? currency = "EUR",
        string? lastChangedAt = "/Date(1760868000000+0000)/") =>
        $$"""{ "d": { "Sku": {{Json(sku)}}, "Price": {{Json(amount)}}, "Currency": {{Json(currency)}}, "LastChangedAt": {{Json(lastChangedAt)}} } }""";

    private static string Json(string? value) => value is null ? "null" : $"\"{value}\"";
}
