using System.Text.RegularExpressions;
using PriceLookup.SapStub.Scenarios;

namespace PriceLookup.SapStub;

// The one OData route of the stub (A9). Credentials and client certificates are ignored (A37).
public static partial class ProductPriceEndpoint
{
    private const string Route = "/sap/opu/odata/sap/ZSD_PRODUCT_PRICE_SRV/{segment}";

    public static void Map(WebApplication app) => app.MapGet(Route, HandleAsync);

    private static async Task HandleAsync(string segment, HttpContext context, ScenarioCatalog scenarios, CallLog log)
    {
        var match = ProductPriceKey().Match(segment);
        if (!match.Success)
        {
            log.Rejected(segment);
            await ODataPayloads.BadRequest(segment).ExecuteAsync(context);
            return;
        }

        var sku = match.Groups["sku"].Value;
        await RespondAsync(log.Record(sku), scenarios.For(sku), context, log);
    }

    private static async Task RespondAsync(StubCall call, IStubScenario scenario, HttpContext context, CallLog log)
    {
        try
        {
            var result = await scenario.RespondAsync(call, context);
            await result.ExecuteAsync(context);
            log.Responded(call, context.Response.StatusCode);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The API gave up on this attempt (attempt or total timeout); nobody is left to answer.
            log.Aborted(call);
        }
    }

    [GeneratedRegex(@"^ProductPriceSet\('(?<sku>[^']+)'\)$")]
    private static partial Regex ProductPriceKey();
}
