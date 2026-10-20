using System.Diagnostics;
using PriceLookup.Core.Application.Ports;
using PriceLookup.Core.Application.Resilience;
using PriceLookup.Core.Domain;
using PriceLookup.Tests.Fakes;

namespace PriceLookup.Tests.Application;

public sealed class TimeoutErpClientTests
{
    private const string RawSku = "ABC-123";
    private const int TotalTimeoutMs = 100;

    private static readonly Sku AbcSku = Sku.TryParse(RawSku, out var sku) ? sku : throw new InvalidOperationException();

    private readonly ScriptedErpClient erp = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Hanging_lookup_throws_timeout_in_time()
    {
        erp.HangsFor(RawSku);
        var stopwatch = Stopwatch.StartNew();

        var exception = await Assert.ThrowsAsync<ErpException>(() => CreateClient().GetPriceAsync(AbcSku, Ct));

        Assert.Equal(ErpFailureKind.Timeout, exception.Kind);
        Assert.True(stopwatch.ElapsedMilliseconds < TotalTimeoutMs + 300, $"Took {stopwatch.ElapsedMilliseconds} ms.");
    }

    [Fact]
    public async Task Fast_success_passes_through()
    {
        erp.WithPrice(RawSku, 19.99m, "EUR");

        var price = await CreateClient().GetPriceAsync(AbcSku, Ct);

        Assert.NotNull(price);
        Assert.Equal(19.99m, price.Price.Amount);
    }

    [Fact]
    public async Task Caller_token_cancelled_while_hanging_throws_operation_canceled_not_timeout()
    {
        erp.HangsFor(RawSku);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        caller.CancelAfter(20);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateClient().GetPriceAsync(AbcSku, caller.Token));
    }

    private TimeoutErpClient CreateClient() =>
        new(erp, new ErpResilienceOptions { TotalTimeoutMs = TotalTimeoutMs, AttemptTimeoutMs = TotalTimeoutMs });
}
