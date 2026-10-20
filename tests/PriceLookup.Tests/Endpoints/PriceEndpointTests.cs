using System.Net;
using System.Text.Json;
using PriceLookup.Core.Application.Ports;
using PriceLookup.Tests.Fakes;
using PriceLookup.Tests.Infrastructure;

namespace PriceLookup.Tests.Endpoints;

public sealed class PriceEndpointTests
{
    private static readonly DateTimeOffset LastUpdated = new(2026, 10, 15, 8, 30, 0, TimeSpan.Zero);

    private readonly ScriptedErpClient erp = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Found_sku_returns_200_with_the_price()
    {
        erp.WithPrice("ABC-123", 19.99m, "EUR", LastUpdated);

        var response = await GetPriceAsync("ABC-123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await ReadJsonAsync(response);
        Assert.Equal("ABC-123", body.GetProperty("sku").GetString());
        Assert.Equal(19.99m, body.GetProperty("price").GetDecimal());
        Assert.Equal("EUR", body.GetProperty("currency").GetString());
        Assert.Equal(LastUpdated, body.GetProperty("lastUpdated").GetDateTimeOffset());
    }

    [Fact]
    public async Task Lower_case_sku_finds_the_price_and_returns_the_normalised_sku()
    {
        erp.WithPrice("ABC-123", 19.99m, "EUR");

        var response = await GetPriceAsync("abc-123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("ABC-123", body.GetProperty("sku").GetString());
    }

    [Fact]
    public async Task Unknown_sku_returns_404_sku_not_found()
    {
        var response = await GetPriceAsync("NOPE-1");

        var problem = await AssertProblemAsync(response, HttpStatusCode.NotFound, "sku_not_found");
        Assert.Equal("SKU not found", problem.GetProperty("title").GetString());
        Assert.Contains("NOPE-1", problem.GetProperty("detail").GetString());
        Assert.Equal("GET /products/NOPE-1/price", problem.GetProperty("instance").GetString());
    }

    [Theory]
    [InlineData("AB%20C")]
    [InlineData("-ABC")]
    [InlineData("AB'C")]
    public async Task Invalid_sku_returns_400_invalid_sku_without_calling_the_erp(string sku)
    {
        var response = await GetPriceAsync(sku);

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_sku");
        Assert.Equal("Invalid SKU", problem.GetProperty("title").GetString());
        Assert.Equal(0, erp.TotalCalls);
    }

    [Theory]
    [InlineData(ErpFailureKind.Transient, HttpStatusCode.BadGateway, "erp_unavailable")]
    [InlineData(ErpFailureKind.Unauthorized, HttpStatusCode.BadGateway, "erp_auth_failed")]
    [InlineData(ErpFailureKind.InvalidResponse, HttpStatusCode.BadGateway, "erp_invalid_response")]
    [InlineData(ErpFailureKind.Timeout, HttpStatusCode.GatewayTimeout, "erp_timeout")]
    [InlineData(ErpFailureKind.AttemptTimeout, HttpStatusCode.GatewayTimeout, "erp_timeout")]
    public async Task Erp_failure_returns_its_status_and_code(ErpFailureKind kind, HttpStatusCode status, string code)
    {
        erp.WithPrice("ABC-123", 19.99m, "EUR").FailsWith("ABC-123", kind);

        var response = await GetPriceAsync("ABC-123");

        var problem = await AssertProblemAsync(response, status, code);
        Assert.Contains("ABC-123", problem.GetProperty("detail").GetString());
    }

    private async Task<HttpResponseMessage> GetPriceAsync(string sku)
    {
        await using var factory = new ApiFactory(erp);
        using var client = factory.CreateClient();
        return await client.GetAsync($"/products/{sku}/price", Ct);
    }

    // Every error response: problem+json with status, code and traceId.
    private static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadJsonAsync(response);
        Assert.Equal((int)status, problem.GetProperty("status").GetInt32());
        Assert.Equal(code, problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
        return problem;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        await using var body = await response.Content.ReadAsStreamAsync(Ct);
        using var document = await JsonDocument.ParseAsync(body, cancellationToken: Ct);
        return document.RootElement.Clone();
    }
}
