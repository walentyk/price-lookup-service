using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceLookup.Core.Application.Ports;
using PriceLookup.Core.Application.Resilience;

namespace PriceLookup.Core.Infrastructure.Sap;

public static class SapServiceCollectionExtensions
{
    public static IServiceCollection AddSapErpClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SapOptions>().Bind(configuration.GetSection(SapOptions.SectionName));
        services.AddSingleton(provider => new SapRequestFactory(GetSapOptions(provider)));
        services.AddHttpClient<SapErpClient>(ConfigureHttpClient)
            .ConfigurePrimaryHttpMessageHandler(() => SapHttpHandlerFactory.Create());

        // Transient, like the typed client inside it, so the HttpClient factory can rotate handlers.
        services.AddTransient<IErpClient>(CreateResilientErpClient);
        return services;
    }

    // All timing belongs to the decorators, so the HttpClient never times out on its own.
    private static void ConfigureHttpClient(IServiceProvider provider, HttpClient client)
    {
        client.BaseAddress = new Uri(GetSapOptions(provider).BaseUrl);
        client.Timeout = Timeout.InfiniteTimeSpan;
    }

    // A17: TimeoutErpClient(RetryingErpClient(AttemptTimeoutErpClient(SapErpClient))).
    private static IErpClient CreateResilientErpClient(IServiceProvider provider)
    {
        var options = provider.GetRequiredService<IOptions<ErpResilienceOptions>>().Value;
        var logger = provider.GetRequiredService<ILogger<RetryingErpClient>>();
        var attempt = new AttemptTimeoutErpClient(provider.GetRequiredService<SapErpClient>(), options);
        return new TimeoutErpClient(new RetryingErpClient(attempt, options, logger), options);
    }

    private static SapOptions GetSapOptions(IServiceProvider provider) =>
        provider.GetRequiredService<IOptions<SapOptions>>().Value;
}
