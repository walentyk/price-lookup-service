namespace PriceLookup.SapStub.Scenarios;

public sealed class MalformedJsonScenario : IStubScenario
{
    public Task<IResult> RespondAsync(StubCall call, HttpContext context) =>
        Task.FromResult(ODataPayloads.Malformed(call.Sku));
}
