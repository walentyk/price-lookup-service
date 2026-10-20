using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PriceLookup.Core.Infrastructure.Sap.Security;
using PriceLookup.Tests.Infrastructure;
using PriceLookup.Tests.Infrastructure.Sap;

namespace PriceLookup.Tests;

// The whole host outside Development: appsettings.Development.json (the stub's http URL) is not loaded,
// so Sap:BaseUrl is the https placeholder from appsettings.json (A8, A24).
public sealed class StartupValidationTests
{
    private const string Password = "s3cret-pfx-password";

    [Fact]
    public void Production_without_a_client_certificate_refuses_to_start()
    {
        using var api = new ApiFactory().WithSetting("environment", "Production");

        var exception = Assert.Throws<OptionsValidationException>(() => api.CreateClient());

        Assert.Contains("Sap:ClientCertificateBase64 is required outside Development.", exception.Message);
    }

    [Fact]
    public async Task Production_with_a_valid_certificate_starts_and_loads_it_once()
    {
        await using var api = new ApiFactory()
            .WithSetting("environment", "Production")
            .WithSetting("Sap:ClientCertificateBase64", TestCertificates.CreateBase64Pfx(Password))
            .WithSetting("Sap:ClientCertificatePassword", Password);
        using var client = api.CreateClient();

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var first = api.Services.GetRequiredService<SapClientCertificate>().Certificate;
        Assert.NotNull(first);
        Assert.True(first.HasPrivateKey);
        Assert.Same(first, api.Services.GetRequiredService<SapClientCertificate>().Certificate);
    }
}
