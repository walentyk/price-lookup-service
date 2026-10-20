using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PriceLookup.Core.Application.GetProductPrice;
using PriceLookup.Core.Application.Resilience;
using PriceLookup.Core.Infrastructure.Sap;

namespace PriceLookup.Core.DependencyInjection;

public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddCore(this IServiceCollection services, IConfiguration configuration)
    {
        AddErpResilienceOptions(services, configuration);
        services.AddSapErpClient(configuration);
        services.AddScoped<GetProductPriceHandler>();
        return services;
    }

    private static void AddErpResilienceOptions(IServiceCollection services, IConfiguration configuration) =>
        services.AddOptions<ErpResilienceOptions>()
            .Bind(configuration.GetSection(ErpResilienceOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options => options.AttemptTimeoutMs <= options.TotalTimeoutMs,
                "Erp:AttemptTimeoutMs must not exceed Erp:TotalTimeoutMs.")
            .ValidateOnStart();
}
