using System.Net.Http.Headers;
using PriceLookup.Core.Domain;

namespace PriceLookup.Core.Infrastructure.Sap;

public sealed class SapRequestFactory(SapOptions options)
{
    // The URI is relative: the typed HttpClient's BaseAddress (Sap:BaseUrl) supplies scheme, host and port.
    public HttpRequestMessage Create(Sku sku)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, CreateUri(sku));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private Uri CreateUri(Sku sku) =>
        new(
            $"{options.ServicePath}ProductPriceSet('{Uri.EscapeDataString(sku.Value)}')?sap-client={options.SapClient}&$format=json",
            UriKind.Relative);
}
