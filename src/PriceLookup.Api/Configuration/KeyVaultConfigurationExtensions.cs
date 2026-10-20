using Azure.Identity;

namespace PriceLookup.Api.Configuration;

public static class KeyVaultConfigurationExtensions
{
    public const string UriKey = "KeyVault:Uri";

    // A22: in Azure the only app setting is KeyVault__Uri and the managed identity reads the vault.
    // Locally and in tests the value is empty, so no Azure call is made.
    public static IConfigurationManager AddKeyVaultWhenConfigured(this IConfigurationManager configuration)
    {
        var uri = configuration[UriKey];
        if (string.IsNullOrWhiteSpace(uri))
        {
            return configuration;
        }

        configuration.AddAzureKeyVault(new Uri(uri), new DefaultAzureCredential());
        return configuration;
    }
}
