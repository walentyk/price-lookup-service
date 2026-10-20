namespace PriceLookup.SapStub.Scenarios;

public sealed class NotFoundScenario : IStubScenario
{
    public Task<IResult> RespondAsync(StubCall call, HttpContext context) =>
        Task.FromResult(ODataPayloads.NotFound());
}
