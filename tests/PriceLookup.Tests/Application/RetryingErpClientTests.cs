using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using PriceLookup.Core.Application.Ports;
using PriceLookup.Core.Application.Resilience;
using PriceLookup.Core.Domain;
using PriceLookup.Tests.Fakes;

namespace PriceLookup.Tests.Application;

public sealed class RetryingErpClientTests
{
    private const string RawSku = "ABC-123";

    // The Windows timer tick is about 15.6 ms, so a pause measured with Stopwatch can end up to one tick early.
    private const int TimerToleranceMs = 16;

    private static readonly Sku AbcSku = Sku.TryParse(RawSku, out var sku) ? sku : throw new InvalidOperationException();

    private readonly ScriptedErpClient erp = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Success_on_the_first_call_makes_one_call()
    {
        erp.WithPrice(RawSku, 19.99m, "EUR");

        var price = await CreateClient().GetPriceAsync(AbcSku, Ct);

        Assert.NotNull(price);
        Assert.Equal(1, erp.TotalCalls);
    }

    [Fact]
    public async Task Transient_twice_then_success_makes_three_calls()
    {
        erp.WithPrice(RawSku, 19.99m, "EUR").FailsWith(RawSku, ErpFailureKind.Transient, times: 2);

        var price = await CreateClient().GetPriceAsync(AbcSku, Ct);

        Assert.NotNull(price);
        Assert.Equal(3, erp.TotalCalls);
    }

    [Fact]
    public async Task Attempt_timeout_once_then_success_makes_two_calls()
    {
        erp.WithPrice(RawSku, 19.99m, "EUR").FailsWith(RawSku, ErpFailureKind.AttemptTimeout, times: 1);

        var price = await CreateClient().GetPriceAsync(AbcSku, Ct);

        Assert.NotNull(price);
        Assert.Equal(2, erp.TotalCalls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public async Task Transient_always_throws_transient_after_retry_count_plus_one_calls(int retryCount)
    {
        erp.FailsWith(RawSku, ErpFailureKind.Transient);

        var exception = await Assert.ThrowsAsync<ErpException>(
            () => CreateClient(retryCount: retryCount).GetPriceAsync(AbcSku, Ct));

        Assert.Equal(ErpFailureKind.Transient, exception.Kind);
        Assert.Equal(retryCount + 1, erp.TotalCalls);
    }

    [Fact]
    public async Task Attempt_timeout_always_throws_attempt_timeout_after_retry_count_plus_one_calls()
    {
        erp.FailsWith(RawSku, ErpFailureKind.AttemptTimeout);

        var exception = await Assert.ThrowsAsync<ErpException>(() => CreateClient().GetPriceAsync(AbcSku, Ct));

        Assert.Equal(ErpFailureKind.AttemptTimeout, exception.Kind);
        Assert.Equal(4, erp.TotalCalls);
    }

    [Theory]
    [InlineData(ErpFailureKind.Unauthorized)]
    [InlineData(ErpFailureKind.InvalidResponse)]
    public async Task Non_retryable_failure_is_thrown_after_one_call(ErpFailureKind kind)
    {
        erp.WithPrice(RawSku, 19.99m, "EUR").FailsWith(RawSku, kind, times: 1);

        var exception = await Assert.ThrowsAsync<ErpException>(() => CreateClient().GetPriceAsync(AbcSku, Ct));

        Assert.Equal(kind, exception.Kind);
        Assert.Equal(1, erp.TotalCalls);
    }

    [Fact]
    public async Task Not_found_returns_null_after_one_call()
    {
        var price = await CreateClient().GetPriceAsync(AbcSku, Ct);

        Assert.Null(price);
        Assert.Equal(1, erp.TotalCalls);
    }

    [Fact]
    public async Task Pause_between_attempts_is_respected()
    {
        erp.WithPrice(RawSku, 19.99m, "EUR").FailsWith(RawSku, ErpFailureKind.Transient, times: 2);
        var stopwatch = Stopwatch.StartNew();

        var price = await CreateClient(retryDelayMs: 100).GetPriceAsync(AbcSku, Ct);

        Assert.NotNull(price);
        Assert.True(stopwatch.ElapsedMilliseconds >= 200 - 2 * TimerToleranceMs, $"Took {stopwatch.ElapsedMilliseconds} ms.");
    }

    [Fact]
    public async Task Caller_token_cancelled_during_the_pause_throws_operation_canceled()
    {
        erp.FailsWith(RawSku, ErpFailureKind.Transient);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        caller.CancelAfter(50);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateClient(retryDelayMs: 5000).GetPriceAsync(AbcSku, caller.Token));

        Assert.Equal(1, erp.TotalCalls);
    }

    private RetryingErpClient CreateClient(int retryCount = 3, int retryDelayMs = 10) =>
        new(
            erp,
            new ErpResilienceOptions { RetryCount = retryCount, RetryDelayMs = retryDelayMs },
            NullLogger<RetryingErpClient>.Instance);
}
