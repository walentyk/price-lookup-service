namespace PriceLookup.SapStub.Scenarios;

// Longer than any attempt timeout: the API always gives up first and disconnects.
public sealed class SlowScenario : IStubScenario
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    public async Task<IResult> RespondAsync(StubCall call, HttpContext context)
    {
        await Task.Delay(Wait, context.RequestAborted);
        return ODataPayloads.Price(call.Sku, 9.99m, "EUR");
    }
}
