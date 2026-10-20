using Microsoft.Extensions.Logging;
using PriceLookup.Core.Application.Ports;
using PriceLookup.Core.Domain;

namespace PriceLookup.Core.Application.GetProductPrice;

public sealed class GetProductPriceHandler(IErpClient erpClient, ILogger<GetProductPriceHandler> logger)
{
    public async Task<PriceLookupResult> HandleAsync(string? rawSku, CancellationToken cancellationToken)
    {
        if (!Sku.TryParse(rawSku, out var sku))
        {
            return InvalidSku(rawSku);
        }

        return await LookUpAsync(sku, cancellationToken);
    }

    // OperationCanceledException is deliberately not caught: a cancelled request is not an ERP failure (A18).
    private async Task<PriceLookupResult> LookUpAsync(Sku sku, CancellationToken cancellationToken)
    {
        try
        {
            var price = await erpClient.GetPriceAsync(sku, cancellationToken);
            return price is null ? NotFound(sku) : Found(price);
        }
        catch (ErpException exception)
        {
            return Failed(sku, exception);
        }
    }

    private PriceLookupResult InvalidSku(string? rawSku)
    {
        logger.LogInformation("Rejected invalid SKU {RawSku}", rawSku);
        return PriceLookupResult.Failed(PriceLookupError.InvalidSku);
    }

    private PriceLookupResult Found(ProductPrice price)
    {
        logger.LogInformation("Price found for SKU {Sku}", price.Sku.Value);
        return PriceLookupResult.Found(price);
    }

    private PriceLookupResult NotFound(Sku sku)
    {
        logger.LogInformation("SKU {Sku} not found in the ERP", sku.Value);
        return PriceLookupResult.Failed(PriceLookupError.SkuNotFound);
    }

    private PriceLookupResult Failed(Sku sku, ErpException exception)
    {
        var error = ErpFailureMapper.Map(exception.Kind);
        logger.LogWarning(exception, "ERP lookup for SKU {Sku} failed with {FailureKind}, result {Error}", sku.Value, exception.Kind, error);
        return PriceLookupResult.Failed(error);
    }
}
