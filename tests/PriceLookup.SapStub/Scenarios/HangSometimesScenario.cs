namespace PriceLookup.SapStub.Scenarios;

// Hangs on calls 1, 4, 7, ... so the API's first attempt hits the attempt timeout and the retry succeeds.
public sealed class HangSometimesScenario : IStubScenario
{
    private static readonly TimeSpan Hang = TimeSpan.FromSeconds(10);

    public async Task<IResult> RespondAsync(StubCall call, HttpContext context)
    {
        if (call.Number % 3 == 1)
        {
            await Task.Delay(Hang, context.RequestAborted);
        }

        return ODataPayloads.Price(call.Sku, 2.50m, "EUR");
    }
}
