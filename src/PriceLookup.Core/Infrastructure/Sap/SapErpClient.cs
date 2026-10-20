using System.Net.Http.Json;
using System.Text.Json;
using PriceLookup.Core.Application.Ports;
using PriceLookup.Core.Domain;
using PriceLookup.Core.Infrastructure.Sap.OData;

namespace PriceLookup.Core.Infrastructure.Sap;

// Exactly one HTTP call per invocation. Retries and timeouts belong to the decorators around this client,
// so OperationCanceledException is never caught here.
public sealed class SapErpClient(HttpClient httpClient, SapRequestFactory requestFactory) : IErpClient
{
    public async Task<ProductPrice?> GetPriceAsync(Sku sku, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(sku, cancellationToken);
        if (SapResponseClassifier.Classify(response.StatusCode) == SapResponseOutcome.NotFound)
        {
            return null;
        }

        var dto = await ReadDtoAsync(response, cancellationToken);
        return SapPriceMapper.ToProductPrice(dto, sku);
    }

    private async Task<HttpResponseMessage> SendAsync(Sku sku, CancellationToken cancellationToken)
    {
        using var request = requestFactory.Create(sku);
        try
        {
            return await httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new ErpException(ErpFailureKind.Transient, $"SAP request for SKU '{sku}' failed.", exception);
        }
    }

    private static async Task<SapProductPriceDto> ReadDtoAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var envelope = await response.Content.ReadFromJsonAsync<ODataV2Envelope>(cancellationToken);
            return envelope?.D ?? throw new ErpException(ErpFailureKind.InvalidResponse, "SAP response has no 'd' object.");
        }
        catch (JsonException exception)
        {
            throw new ErpException(ErpFailureKind.InvalidResponse, "SAP response is not valid JSON.", exception);
        }
    }
}
