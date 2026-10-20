namespace PriceLookup.SapStub.Scenarios;

// What the ICF answers when it cannot map the caller to an SAP user.
public sealed class UnauthorizedScenario : IStubScenario
{
    public Task<IResult> RespondAsync(StubCall call, HttpContext context)
    {
        context.Response.Headers.WWWAuthenticate = "Basic realm=\"SAP NetWeaver Application Server [S4H/100]\"";
        return Task.FromResult(ODataPayloads.Error(StatusCodes.Status401Unauthorized, "/IWFND/CM_CONSUMER/101", "Logon failed"));
    }
}
