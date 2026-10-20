using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using PriceLookup.Tests.Infrastructure;

namespace PriceLookup.Tests.EndToEnd;

// The real chain TimeoutErpClient(RetryingErpClient(AttemptTimeoutErpClient(SapErpClient))) against the stub (A35).
// Each magic SKU is used by one test only, so the per-SKU call counts of the shared stub stay exact.
public sealed class StubScenarioTests(SapStubFixture stub) : IClassFixture<SapStubFixture>
{
    private const int TotalTimeoutMs = 600;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("ABC-123", "19.99", "EUR")]
    [InlineData("XYZ-999", "1250.00", "PLN")]
    public async Task Found_sku_returns_200_with_the_stub_price(string sku, string price, string currency)
    {
        var response = await GetPriceAsync(sku);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(sku, body.GetProperty("sku").GetString());
        Assert.Equal(decimal.Parse(price, CultureInfo.InvariantCulture), body.GetProperty("price").GetDecimal());
        Assert.Equal(currency, body.GetProperty("currency").GetString());
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1760868000000), body.GetProperty("lastUpdated").GetDateTimeOffset());
    }

    [Fact]
    public async Task Unknown_sku_returns_404_sku_not_found()
    {
        var response = await GetPriceAsync("NOPE-1");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "sku_not_found");
        Assert.Equal(1, stub.Calls.CallsFor("NOPE-1"));
    }

    [Fact]
    public async Task Flaky_sku_returns_200_on_the_third_call()
    {
        var response = await GetPriceAsync("FLAKY-1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, stub.Calls.CallsFor("FLAKY-1"));
    }

    [Fact]
    public async Task Failing_sku_returns_502_erp_unavailable_after_1_plus_n_calls()
    {
        var response = await GetPriceAsync("FAIL-1");

        await AssertProblemAsync(response, HttpStatusCode.BadGateway, "erp_unavailable");
        Assert.Equal(4, stub.Calls.CallsFor("FAIL-1"));
    }

    [Fact]
    public async Task Rejected_certificate_returns_502_erp_auth_failed_after_1_call()
    {
        var response = await GetPriceAsync("AUTH-1");

        await AssertProblemAsync(response, HttpStatusCode.BadGateway, "erp_auth_failed");
        Assert.Equal(1, stub.Calls.CallsFor("AUTH-1"));
    }

    [Fact]
    public async Task Malformed_json_returns_502_erp_invalid_response_after_1_call()
    {
        var response = await GetPriceAsync("BROKEN-1");

        await AssertProblemAsync(response, HttpStatusCode.BadGateway, "erp_invalid_response");
        Assert.Equal(1, stub.Calls.CallsFor("BROKEN-1"));
    }

    [Fact]
    public async Task Hanging_first_call_is_cut_by_the_attempt_timeout_and_the_retry_returns_200()
    {
        var response = await GetPriceAsync("HANG-1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(2.50m, body.GetProperty("price").GetDecimal());
        Assert.Equal(2, stub.Calls.CallsFor("HANG-1"));
    }

    [Fact]
    public async Task Slow_sku_returns_504_erp_timeout_within_the_total_timeout()
    {
        await using var api = CreateApi();
        using var client = api.CreateClient();

        var started = Stopwatch.GetTimestamp();
        var response = await client.GetAsync("/products/SLOW-1/price", Ct);

        Assert.True(Stopwatch.GetElapsedTime(started) < TimeSpan.FromMilliseconds(TotalTimeoutMs + 300));
        await AssertProblemAsync(response, HttpStatusCode.GatewayTimeout, "erp_timeout");
        Assert.True(stub.Calls.CallsFor("SLOW-1") >= 2);
    }

    [Fact]
    public async Task Invalid_sku_returns_400_invalid_sku_without_calling_the_stub()
    {
        var callsBefore = stub.Calls.TotalCalls;

        var response = await GetPriceAsync("bad%20sku");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_sku");
        Assert.Equal(callsBefore, stub.Calls.TotalCalls);
    }

    // Small timings so the slow scenarios stay well under 1 s; the attempt timeout is far below the stub's 10 s waits.
    private ApiFactory CreateApi() =>
        new ApiFactory()
            .UsingRealErpClient()
            .WithSetting("Sap:BaseUrl", stub.BaseUrl)
            .WithSetting("Erp:TotalTimeoutMs", TotalTimeoutMs.ToString(CultureInfo.InvariantCulture))
            .WithSetting("Erp:AttemptTimeoutMs", "150")
            .WithSetting("Erp:RetryDelayMs", "10")
            .WithSetting("Erp:RetryCount", "3");

    private async Task<HttpResponseMessage> GetPriceAsync(string sku)
    {
        await using var api = CreateApi();
        using var client = api.CreateClient();
        return await client.GetAsync($"/products/{sku}/price", Ct);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadJsonAsync(response);
        Assert.Equal(code, problem.GetProperty("code").GetString());
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        await using var body = await response.Content.ReadAsStreamAsync(Ct);
        using var document = await JsonDocument.ParseAsync(body, cancellationToken: Ct);
        return document.RootElement.Clone();
    }
}
