using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceLookup.Core.Application.Ports;
using PriceLookup.Core.Application.Resilience;
using PriceLookup.Core.Infrastructure.Sap.Security;

namespace PriceLookup.Core.Infrastructure.Sap;

public static class SapServiceCollectionExtensions
{
    public static IServiceCollection AddSapErpClient(this IServiceCollection services, IConfiguration configuration)
    {
        AddSapOptions(services, configuration);
        services.AddSingleton<SapClientCertificate>();
        services.AddSingleton(provider => new SapRequestFactory(GetSapOptions(provider)));
        services.AddHttpClient<SapErpClient>(ConfigureHttpClient)
            .ConfigurePrimaryHttpMessageHandler(CreatePrimaryHandler);

        // Transient, like the typed client inside it, so the HttpClient factory can rotate handlers.
        services.AddTransient<IErpClient>(CreateResilientErpClient);
        return services;
    }

    // A24: invalid SAP settings stop the service at startup instead of failing the first request.
    private static void AddSapOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SapOptions>()
            .Bind(configuration.GetSection(SapOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<SapOptions>, SapOptionsValidator>();
    }

    // All timing belongs to the decorators, so the HttpClient never times out on its own.
    private static void ConfigureHttpClient(IServiceProvider provider, HttpClient client)
    {
        client.BaseAddress = new Uri(GetSapOptions(provider).BaseUrl);
        client.Timeout = Timeout.InfiniteTimeSpan;
    }

    private static SocketsHttpHandler CreatePrimaryHandler(IServiceProvider provider) =>
        SapHttpHandlerFactory.Create(provider.GetRequiredService<SapClientCertificate>().Certificate);

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
