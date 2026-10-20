using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PriceLookup.Core.Application.GetProductPrice;

namespace PriceLookup.Core.DependencyInjection;

public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<GetProductPriceHandler>();
        return services;
    }
}
