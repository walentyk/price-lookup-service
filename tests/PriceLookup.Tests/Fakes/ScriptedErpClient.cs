using System.Diagnostics;
using PriceLookup.Core.Application.Ports;
using PriceLookup.Core.Domain;

namespace PriceLookup.Tests.Fakes;

public sealed class ScriptedErpClient : IErpClient
{
    public static readonly DateTimeOffset DefaultLastUpdated = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly Lock gate = new();
    private readonly Dictionary<string, ProductPrice> prices = [];
    private readonly Dictionary<string, Queue<ScriptedStep>> steps = [];
    private readonly Dictionary<string, int> callsBySku = [];
    private int totalCalls;

    public int TotalCalls
    {
        get { lock (gate) { return totalCalls; } }
    }

    public ScriptedErpClient WithPrice(string sku, decimal amount, string currency, DateTimeOffset? lastUpdated = null)
    {
        var price = CreatePrice(sku, amount, currency, lastUpdated ?? DefaultLastUpdated);
        lock (gate) { prices[price.Sku.Value] = price; }
        return this;
    }

    public ScriptedErpClient FailsWith(string sku, ErpFailureKind kind, int times = int.MaxValue) =>
        AddStep(sku, new ScriptedStep(kind, times));

    public ScriptedErpClient HangsFor(string sku, int times = int.MaxValue) =>
        AddStep(sku, new ScriptedStep(null, times));

    public int CallsFor(string sku)
    {
        var key = Key(sku);
        lock (gate) { return callsBySku.GetValueOrDefault(key); }
    }

    public async Task<ProductPrice?> GetPriceAsync(Sku sku, CancellationToken cancellationToken)
    {
        var (step, price) = RecordCall(sku.Value);
        cancellationToken.ThrowIfCancellationRequested();
        if (step is null)
        {
            return price;
        }

        if (step.Failure is { } kind)
        {
            throw new ErpException(kind, $"Scripted {kind} failure for SKU '{sku}'.");
        }

        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new UnreachableException();
    }

    private ScriptedErpClient AddStep(string sku, ScriptedStep step)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(step.Remaining);
        var key = Key(sku);
        lock (gate)
        {
            steps.TryAdd(key, new Queue<ScriptedStep>());
            steps[key].Enqueue(step);
        }

        return this;
    }

    private (ScriptedStep? Step, ProductPrice? Price) RecordCall(string sku)
    {
        lock (gate)
        {
            totalCalls++;
            callsBySku[sku] = callsBySku.GetValueOrDefault(sku) + 1;
            return (TakeStep(sku), prices.GetValueOrDefault(sku));
        }
    }

    // Caller holds the lock. The head step is used until its count runs out, then the next one takes over.
    private ScriptedStep? TakeStep(string sku)
    {
        if (!steps.TryGetValue(sku, out var queue) || queue.Count == 0)
        {
            return null;
        }

        var step = queue.Peek();
        if (--step.Remaining == 0)
        {
            queue.Dequeue();
        }

        return step;
    }

    private static ProductPrice CreatePrice(string sku, decimal amount, string currency, DateTimeOffset lastUpdated)
    {
        if (Sku.TryParse(sku, out var parsedSku)
            && CurrencyCode.TryParse(currency, out var code)
            && Price.TryCreate(amount, code, out var price))
        {
            return new ProductPrice(parsedSku, price, lastUpdated);
        }

        throw new ArgumentException($"Cannot script price {amount} {currency} for SKU '{sku}'.");
    }

    private static string Key(string sku) =>
        Sku.TryParse(sku, out var parsed) ? parsed.Value : throw new ArgumentException($"Invalid SKU '{sku}'.", nameof(sku));

    // Failure null means the step hangs until the token is cancelled.
    private sealed class ScriptedStep(ErpFailureKind? failure, int times)
    {
        public ErpFailureKind? Failure { get; } = failure;

        public int Remaining { get; set; } = times;
    }
}
