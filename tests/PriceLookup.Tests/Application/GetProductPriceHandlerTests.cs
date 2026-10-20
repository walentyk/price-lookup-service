using Microsoft.Extensions.Logging.Abstractions;
using PriceLookup.Core.Application.GetProductPrice;
using PriceLookup.Core.Application.Ports;
using PriceLookup.Tests.Fakes;

namespace PriceLookup.Tests.Application;

public sealed class GetProductPriceHandlerTests
{
    private readonly ScriptedErpClient erp = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Found_sku_returns_the_price()
    {
        erp.WithPrice("ABC-123", 19.99m, "EUR");

        var result = await CreateHandler().HandleAsync("ABC-123", Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("ABC-123", result.Price.Sku.Value);
        Assert.Equal(19.99m, result.Price.Price.Amount);
        Assert.Equal("EUR", result.Price.Price.Currency.Value);
        Assert.Equal(ScriptedErpClient.DefaultLastUpdated, result.Price.LastUpdated);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Lower_case_input_is_normalised_in_the_result()
    {
        erp.WithPrice("ABC-123", 19.99m, "EUR");

        var result = await CreateHandler().HandleAsync("  abc-123 ", Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("ABC-123", result.Price.Sku.Value);
        Assert.Equal(1, erp.CallsFor("ABC-123"));
    }

    [Fact]
    public async Task Unknown_sku_returns_sku_not_found()
    {
        var result = await CreateHandler().HandleAsync("NOPE-1", Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(PriceLookupError.SkuNotFound, result.Error);
        Assert.Equal(1, erp.TotalCalls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("AB C")]
    [InlineData("-ABC")]
    public async Task Invalid_sku_returns_invalid_sku_without_calling_the_erp(string? rawSku)
    {
        var result = await CreateHandler().HandleAsync(rawSku, Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(PriceLookupError.InvalidSku, result.Error);
        Assert.Equal(0, erp.TotalCalls);
    }

    [Theory]
    [InlineData(ErpFailureKind.Transient, PriceLookupError.ErpUnavailable)]
    [InlineData(ErpFailureKind.Unauthorized, PriceLookupError.ErpAuthFailed)]
    [InlineData(ErpFailureKind.InvalidResponse, PriceLookupError.ErpInvalidResponse)]
    [InlineData(ErpFailureKind.AttemptTimeout, PriceLookupError.ErpTimeout)]
    [InlineData(ErpFailureKind.Timeout, PriceLookupError.ErpTimeout)]
    public async Task Erp_failure_kind_maps_to_its_lookup_error(ErpFailureKind kind, PriceLookupError expected)
    {
        erp.WithPrice("ABC-123", 19.99m, "EUR").FailsWith("ABC-123", kind);

        var result = await CreateHandler().HandleAsync("ABC-123", Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.Error);
    }

    [Fact]
    public async Task Cancelled_token_throws_operation_canceled_and_is_not_mapped()
    {
        erp.WithPrice("ABC-123", 19.99m, "EUR");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateHandler().HandleAsync("ABC-123", cancellation.Token));
    }

    private GetProductPriceHandler CreateHandler() => new(erp, NullLogger<GetProductPriceHandler>.Instance);
}
