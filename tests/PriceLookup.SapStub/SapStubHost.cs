using PriceLookup.SapStub.Scenarios;

namespace PriceLookup.SapStub;

// Builds the stub app without starting it, so tests can host it in-process on a free loopback port.
public static class SapStubHost
{
    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
        builder.Services.AddSingleton<CallLog>();
        builder.Services.AddSingleton<ScenarioCatalog>();

        var app = builder.Build();
        ProductPriceEndpoint.Map(app);
        return app;
    }
}
