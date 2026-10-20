namespace PriceLookup.SapStub.Scenarios;

// 503 unless the call number is a multiple of 3, so the API succeeds on its third attempt.
public sealed class FlakyScenario : IStubScenario
{
    public Task<IResult> RespondAsync(StubCall call, HttpContext context) =>
        Task.FromResult(call.Number % 3 == 0
            ? ODataPayloads.Price(call.Sku, 5.00m, "EUR")
            : ODataPayloads.Error(StatusCodes.Status503ServiceUnavailable, "/IWFND/CM_BEC/026", "Service temporarily unavailable"));
}
