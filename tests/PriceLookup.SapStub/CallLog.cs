using System.Collections.Concurrent;
using PriceLookup.SapStub.Scenarios;

namespace PriceLookup.SapStub;

// Per-SKU call counter: scenarios use the call number, tests assert how many attempts reached the stub.
public sealed class CallLog(ILogger<CallLog> logger)
{
    private readonly ConcurrentDictionary<string, int> calls = new(StringComparer.Ordinal);

    public int TotalCalls => calls.Values.Sum();

    public int CallsFor(string sku) => calls.GetValueOrDefault(sku);

    public StubCall Record(string sku) => new(sku, calls.AddOrUpdate(sku, 1, (_, count) => count + 1));

    public void Responded(StubCall call, int statusCode) =>
        logger.LogInformation("SKU {Sku}, call {Call}: {StatusCode}", call.Sku, call.Number, statusCode);

    public void Aborted(StubCall call) =>
        logger.LogInformation("SKU {Sku}, call {Call}: client disconnected", call.Sku, call.Number);

    public void Rejected(string segment) =>
        logger.LogInformation("Segment '{Segment}': 400", segment);
}
