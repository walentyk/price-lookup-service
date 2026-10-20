namespace PriceLookup.SapStub.Scenarios;

public sealed class AlwaysFailsScenario : IStubScenario
{
    public Task<IResult> RespondAsync(StubCall call, HttpContext context) =>
        Task.FromResult(ODataPayloads.Error(StatusCodes.Status500InternalServerError, "/IWBEP/CM_MGW_RT/000", "Internal server error"));
}
