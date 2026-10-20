using System.Diagnostics;
using PriceLookup.Core.Application.Ports;
using PriceLookup.Core.Application.Resilience;
using PriceLookup.Core.Domain;
using PriceLookup.Tests.Fakes;

namespace PriceLookup.Tests.Application;

public sealed class AttemptTimeoutErpClientTests
{
    private const string RawSku = "ABC-123";
    private const int AttemptTimeoutMs = 100;

    private static readonly Sku AbcSku = Sku.TryParse(RawSku, out var sku) ? sku : throw new InvalidOperationException();

    private readonly ScriptedErpClient erp = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Hanging_attempt_throws_attempt_timeout_in_time()
    {
        erp.HangsFor(RawSku);
        var stopwatch = Stopwatch.StartNew();

        var exception = await Assert.ThrowsAsync<ErpException>(() => CreateClient().GetPriceAsync(AbcSku, Ct));

        Assert.Equal(ErpFailureKind.AttemptTimeout, exception.Kind);
        Assert.True(stopwatch.ElapsedMilliseconds < AttemptTimeoutMs + 300, $"Took {stopwatch.ElapsedMilliseconds} ms.");
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
    public async Task Not_found_passes_through_as_null()
    {
        var price = await CreateClient().GetPriceAsync(AbcSku, Ct);

        Assert.Null(price);
        Assert.Equal(1, erp.TotalCalls);
    }

    [Fact]
    public async Task Incoming_token_cancelled_while_hanging_throws_operation_canceled_not_attempt_timeout()
    {
        erp.HangsFor(RawSku);
        using var incoming = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        incoming.CancelAfter(20);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateClient().GetPriceAsync(AbcSku, incoming.Token));
    }

    private AttemptTimeoutErpClient CreateClient() =>
        new(erp, new ErpResilienceOptions { AttemptTimeoutMs = AttemptTimeoutMs });
}
