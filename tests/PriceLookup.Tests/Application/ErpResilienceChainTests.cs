using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using PriceLookup.Core.Application.Ports;
using PriceLookup.Core.Application.Resilience;
using PriceLookup.Core.Domain;
using PriceLookup.Tests.Fakes;

namespace PriceLookup.Tests.Application;

// The full chain TimeoutErpClient(RetryingErpClient(AttemptTimeoutErpClient(fake))).
public sealed class ErpResilienceChainTests
{
    private const string RawSku = "ABC-123";

    // The Windows timer tick is about 15.6 ms, so a timeout measured with Stopwatch can fire up to one tick early.
    private const int TimerToleranceMs = 16;

    private static readonly Sku AbcSku = Sku.TryParse(RawSku, out var sku) ? sku : throw new InvalidOperationException();

    private readonly ScriptedErpClient erp = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task First_attempt_hangs_and_the_retry_finds_the_price()
    {
        erp.WithPrice(RawSku, 19.99m, "EUR").HangsFor(RawSku, times: 1);
        var stopwatch = Stopwatch.StartNew();

        var price = await CreateChain(Options()).GetPriceAsync(AbcSku, Ct);

        Assert.NotNull(price);
        Assert.Equal(2, erp.TotalCalls);
        Assert.True(stopwatch.ElapsedMilliseconds >= 100 - TimerToleranceMs, $"Took {stopwatch.ElapsedMilliseconds} ms.");
    }

    [Fact]
    public async Task Always_hanging_ends_with_attempt_timeout_after_four_calls()
    {
        erp.HangsFor(RawSku);

        // 4 attempts of 100 ms take about 500 ms on Windows; 900 ms leaves room so the total timeout never wins here.
        var exception = await Assert.ThrowsAsync<ErpException>(
            () => CreateChain(Options(totalTimeoutMs: 900)).GetPriceAsync(AbcSku, Ct));

        Assert.Equal(ErpFailureKind.AttemptTimeout, exception.Kind);
        Assert.Equal(4, erp.TotalCalls);
    }

    [Fact]
    public async Task Always_hanging_with_a_short_total_timeout_ends_with_timeout()
    {
        erp.HangsFor(RawSku);
        var stopwatch = Stopwatch.StartNew();

        var exception = await Assert.ThrowsAsync<ErpException>(
            () => CreateChain(Options(totalTimeoutMs: 250)).GetPriceAsync(AbcSku, Ct));

        Assert.Equal(ErpFailureKind.Timeout, exception.Kind);
        Assert.True(stopwatch.ElapsedMilliseconds < 250 + 300, $"Took {stopwatch.ElapsedMilliseconds} ms.");
        Assert.True(erp.TotalCalls >= 2, $"Made {erp.TotalCalls} calls.");
    }

    [Fact]
    public async Task Transient_always_ends_with_transient_after_four_calls()
    {
        erp.FailsWith(RawSku, ErpFailureKind.Transient);

        var exception = await Assert.ThrowsAsync<ErpException>(() => CreateChain(Options()).GetPriceAsync(AbcSku, Ct));

        Assert.Equal(ErpFailureKind.Transient, exception.Kind);
        Assert.Equal(4, erp.TotalCalls);
    }

    [Fact]
    public async Task Total_timeout_ending_during_the_pause_ends_with_timeout()
    {
        erp.FailsWith(RawSku, ErpFailureKind.Transient);
        var options = Options(totalTimeoutMs: 200, retryDelayMs: 1000);

        var exception = await Assert.ThrowsAsync<ErpException>(() => CreateChain(options).GetPriceAsync(AbcSku, Ct));

        Assert.Equal(ErpFailureKind.Timeout, exception.Kind);
        Assert.Equal(1, erp.TotalCalls);
    }

    private static ErpResilienceOptions Options(int totalTimeoutMs = 600, int retryDelayMs = 10) =>
        new() { AttemptTimeoutMs = 100, TotalTimeoutMs = totalTimeoutMs, RetryCount = 3, RetryDelayMs = retryDelayMs };

    private TimeoutErpClient CreateChain(ErpResilienceOptions options)
    {
        var attemptTimeout = new AttemptTimeoutErpClient(erp, options);
        var retrying = new RetryingErpClient(attemptTimeout, options, NullLogger<RetryingErpClient>.Instance);
        return new TimeoutErpClient(retrying, options);
    }
}
