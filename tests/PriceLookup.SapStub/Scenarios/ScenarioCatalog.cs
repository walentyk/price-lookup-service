namespace PriceLookup.SapStub.Scenarios;

// A35: the magic SKUs. Any other SKU is unknown to SAP.
public sealed class ScenarioCatalog
{
    private readonly NotFoundScenario notFound = new();

    private readonly Dictionary<string, IStubScenario> scenarios = new(StringComparer.Ordinal)
    {
        ["ABC-123"] = new FoundScenario(19.99m, "EUR"),
        ["XYZ-999"] = new FoundScenario(1250.00m, "PLN"),
        ["FLAKY-1"] = new FlakyScenario(),
        ["FAIL-1"] = new AlwaysFailsScenario(),
        ["AUTH-1"] = new UnauthorizedScenario(),
        ["BROKEN-1"] = new MalformedJsonScenario(),
        ["HANG-1"] = new HangSometimesScenario(),
        ["SLOW-1"] = new SlowScenario(),
    };

    public IStubScenario For(string sku) => scenarios.GetValueOrDefault(sku, notFound);
}
