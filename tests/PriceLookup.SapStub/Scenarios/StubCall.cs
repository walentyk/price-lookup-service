namespace PriceLookup.SapStub.Scenarios;

// Number is the 1-based count of calls for this SKU since the stub started.
public sealed record StubCall(string Sku, int Number);
