namespace PriceLookup.SapStub.Scenarios;

public sealed class FoundScenario(decimal amount, string currency) : IStubScenario
{
    public Task<IResult> RespondAsync(StubCall call, HttpContext context) =>
        Task.FromResult(ODataPayloads.Price(call.Sku, amount, currency));
}
