using PriceLookup.Core.Domain;
using PriceLookup.Core.Infrastructure.Sap;

namespace PriceLookup.Tests.Infrastructure.Sap;

public sealed class SapRequestFactoryTests
{
    private static readonly Sku AbcSku = Sku.TryParse("ABC-123", out var sku) ? sku : throw new InvalidOperationException();

    [Fact]
    public void Default_options_build_the_contract_request()
    {
        using var request = new SapRequestFactory(new SapOptions()).Create(AbcSku);

        var absolute = new Uri(new Uri("https://sap.example.invalid"), request.RequestUri!);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(
            "https://sap.example.invalid/sap/opu/odata/sap/ZSD_PRODUCT_PRICE_SRV/ProductPriceSet('ABC-123')?sap-client=100&$format=json",
            absolute.ToString());
    }

    [Fact]
    public void Request_accepts_json()
    {
        using var request = new SapRequestFactory(new SapOptions()).Create(AbcSku);

        var accept = Assert.Single(request.Headers.Accept);
        Assert.Equal("application/json", accept.MediaType);
    }

    [Fact]
    public void Service_path_and_sap_client_come_from_the_options()
    {
        var options = new SapOptions { ServicePath = "/custom/service/", SapClient = "200" };

        using var request = new SapRequestFactory(options).Create(AbcSku);

        Assert.Equal("/custom/service/ProductPriceSet('ABC-123')?sap-client=200&$format=json", request.RequestUri?.ToString());
    }
}
