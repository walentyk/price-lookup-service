using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using PriceLookup.SapStub;

namespace PriceLookup.Tests.EndToEnd;

// The real stub app on a free port of the loopback interface, one per test class.
public sealed class SapStubFixture : IAsyncLifetime
{
    private readonly WebApplication stub = SapStubHost.Build(
        ["--urls", "http://127.0.0.1:0", "--Logging:LogLevel:Default", "Warning"]);

    public string BaseUrl => stub.Urls.Single();

    public CallLog Calls => stub.Services.GetRequiredService<CallLog>();

    public async ValueTask InitializeAsync() => await stub.StartAsync(TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync()
    {
        await stub.StopAsync(TestContext.Current.CancellationToken);
        await stub.DisposeAsync();
    }
}
