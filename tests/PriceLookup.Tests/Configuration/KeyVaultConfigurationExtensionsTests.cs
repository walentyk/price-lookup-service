using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PriceLookup.Api.Configuration;
using PriceLookup.Tests.Infrastructure;

namespace PriceLookup.Tests.Configuration;

// Only the "skipped" path is tested: adding the provider would make a real call to Azure (A22).
public sealed class KeyVaultConfigurationExtensionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Key_vault_is_skipped_when_the_uri_is_empty(string? uri)
    {
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?> { [KeyVaultConfigurationExtensions.UriKey] = uri });
        var sourcesBefore = configuration.Sources.ToList();

        configuration.AddKeyVaultWhenConfigured();

        Assert.Equal(sourcesBefore, configuration.Sources);
        Assert.DoesNotContain(configuration.Sources, source => source is AzureKeyVaultConfigurationSource);
    }

    [Fact]
    public void Running_app_with_default_settings_has_no_key_vault_provider()
    {
        using var api = new ApiFactory();

        var configuration = (IConfigurationRoot)api.Services.GetRequiredService<IConfiguration>();

        Assert.DoesNotContain(configuration.Providers, provider => provider is AzureKeyVaultConfigurationProvider);
    }
}
