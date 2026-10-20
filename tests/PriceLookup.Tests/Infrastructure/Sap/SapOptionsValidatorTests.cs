using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PriceLookup.Core.Infrastructure.Sap;
using PriceLookup.Tests.Fakes;

namespace PriceLookup.Tests.Infrastructure.Sap;

public sealed class SapOptionsValidatorTests
{
    private const string Password = "s3cret-pfx-password";

    [Fact]
    public void Http_base_url_fails_in_production()
    {
        var options = ProductionReadyOptions();
        options.BaseUrl = "http://sap.example.invalid";

        var failure = AssertSingleFailure(Environments.Production, options);

        Assert.Equal("Sap:BaseUrl must use https (http is allowed only in Development).", failure);
    }

    [Fact]
    public void Http_base_url_without_certificate_passes_in_development()
    {
        var options = new SapOptions { BaseUrl = "http://localhost:5199" };

        Assert.True(Validate(Environments.Development, options).Succeeded);
    }

    [Fact]
    public void Missing_certificate_fails_in_production()
    {
        var options = ProductionReadyOptions();
        options.ClientCertificateBase64 = "";

        var failure = AssertSingleFailure(Environments.Production, options);

        Assert.Equal("Sap:ClientCertificateBase64 is required outside Development.", failure);
    }

    [Fact]
    public void Valid_self_signed_certificate_and_https_pass_in_production()
    {
        Assert.True(Validate(Environments.Production, ProductionReadyOptions()).Succeeded);
    }

    [Fact]
    public void Certificate_that_does_not_load_fails_even_in_development()
    {
        var options = new SapOptions { BaseUrl = "http://localhost:5199", ClientCertificateBase64 = "not base64 !!" };

        var failure = AssertSingleFailure(Environments.Development, options);

        Assert.Equal("Sap:ClientCertificateBase64 is not valid base64.", failure);
    }

    [Fact]
    public void Wrong_password_fails_without_revealing_it()
    {
        var options = ProductionReadyOptions();
        options.ClientCertificatePassword = "wrong-pfx-password";

        var failure = AssertSingleFailure(Environments.Production, options);

        Assert.DoesNotContain("wrong-pfx-password", failure);
        Assert.DoesNotContain(Password, failure);
    }

    [Theory]
    [InlineData("sap.example.invalid")]
    [InlineData("/relative/path")]
    [InlineData("")]
    public void Relative_or_empty_base_url_fails(string baseUrl)
    {
        var options = ProductionReadyOptions();
        options.BaseUrl = baseUrl;

        Assert.False(Validate(Environments.Production, options).Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("10")]
    [InlineData("1000")]
    [InlineData("1a0")]
    public void Sap_client_that_is_not_three_digits_fails(string sapClient)
    {
        var options = ProductionReadyOptions();
        options.SapClient = sapClient;

        Assert.Equal("Sap:SapClient must be exactly three digits.", AssertSingleFailure(Environments.Production, options));
    }

    [Fact]
    public void Empty_service_path_fails()
    {
        var options = ProductionReadyOptions();
        options.ServicePath = " ";

        Assert.Equal("Sap:ServicePath must not be empty.", AssertSingleFailure(Environments.Production, options));
    }

    [Theory]
    [InlineData("sap/opu/odata/sap/ZSD_PRODUCT_PRICE_SRV/")]
    [InlineData("/sap/opu/odata/sap/ZSD_PRODUCT_PRICE_SRV")]
    public void Service_path_without_leading_or_trailing_slash_fails(string servicePath)
    {
        var options = ProductionReadyOptions();
        options.ServicePath = servicePath;

        Assert.Equal("Sap:ServicePath must start and end with \"/\".", AssertSingleFailure(Environments.Production, options));
    }

    [Theory]
    [InlineData("https://sap.example.invalid/sap")]
    [InlineData("https://sap.example.invalid/sap/opu/odata/")]
    [InlineData("https://sap.example.invalid?sap-client=100")]
    [InlineData("https://sap.example.invalid/#top")]
    public void Base_url_with_path_query_or_fragment_fails(string baseUrl)
    {
        var options = ProductionReadyOptions();
        options.BaseUrl = baseUrl;

        Assert.Equal(
            "Sap:BaseUrl must have no path, query or fragment (only scheme, host and port); the path belongs in Sap:ServicePath.",
            AssertSingleFailure(Environments.Production, options));
    }

    [Theory]
    [InlineData("https://sap.example.invalid/")]
    [InlineData("https://sap.example.invalid:44300")]
    public void Base_url_with_only_scheme_host_and_port_passes(string baseUrl)
    {
        var options = ProductionReadyOptions();
        options.BaseUrl = baseUrl;

        Assert.True(Validate(Environments.Production, options).Succeeded);
    }

    private static SapOptions ProductionReadyOptions() => new()
    {
        BaseUrl = "https://sap.example.invalid",
        ClientCertificateBase64 = TestCertificates.CreateBase64Pfx(Password),
        ClientCertificatePassword = Password,
    };

    private static ValidateOptionsResult Validate(string environment, SapOptions options) =>
        new SapOptionsValidator(new FakeHostEnvironment(environment)).Validate(null, options);

    private static string AssertSingleFailure(string environment, SapOptions options)
    {
        var result = Validate(environment, options);

        Assert.True(result.Failed);
        return Assert.Single(result.Failures!);
    }
}
