using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PriceLookup.Core.Application.Ports;
using PriceLookup.Tests.Fakes;

namespace PriceLookup.Tests.Infrastructure;

// Without a fake of its own a test gets a fresh, empty ScriptedErpClient: every SKU is unknown.
// End-to-end tests call UsingRealErpClient() to keep the production chain that AddCore registers.
public sealed class ApiFactory(ScriptedErpClient? erp = null) : WebApplicationFactory<Program>
{
    private readonly ScriptedErpClient erp = erp ?? new ScriptedErpClient();
    private readonly Dictionary<string, string?> settings = [];
    private bool replaceErpClient = true;

    public ApiFactory WithSetting(string key, string? value)
    {
        settings[key] = value;
        return this;
    }

    public ApiFactory UsingRealErpClient()
    {
        replaceErpClient = false;
        return this;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }

        if (replaceErpClient)
        {
            builder.ConfigureTestServices(ReplaceServices);
        }
    }

    private void ReplaceServices(IServiceCollection services)
    {
        services.RemoveAll<IErpClient>();
        services.AddSingleton<IErpClient>(erp);
    }
}
