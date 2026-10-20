namespace PriceLookup.SapStub.Scenarios;

// One behaviour of the stub, selected by a magic SKU (A35).
public interface IStubScenario
{
    Task<IResult> RespondAsync(StubCall call, HttpContext context);
}
