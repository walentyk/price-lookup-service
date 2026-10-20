using Microsoft.Extensions.Logging;
using PriceLookup.Core.Application.Ports;
using PriceLookup.Core.Domain;

namespace PriceLookup.Core.Application.Resilience;

public sealed class RetryingErpClient(IErpClient inner, ErpResilienceOptions options, ILogger<RetryingErpClient> logger)
    : IErpClient
{
    private int Attempts => options.RetryCount + 1;

    // Only retryable failures of the earlier attempts are caught; the last attempt's outcome, whatever it is,
    // leaves unchanged, so it decides between 502 and 504 (A16). OperationCanceledException is never caught.
    public async Task<ProductPrice?> GetPriceAsync(Sku sku, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt < Attempts; attempt++)
        {
            try
            {
                return await AttemptAsync(sku, attempt, cancellationToken);
            }
            catch (ErpException exception) when (exception.IsRetryable)
            {
                await Task.Delay(options.RetryDelayMs, cancellationToken);
            }
        }

        return await AttemptAsync(sku, Attempts, cancellationToken);
    }

    private async Task<ProductPrice?> AttemptAsync(Sku sku, int attempt, CancellationToken cancellationToken)
    {
        try
        {
            return await inner.GetPriceAsync(sku, cancellationToken);
        }
        catch (ErpException exception)
        {
            logger.LogWarning(
                "ERP attempt {Attempt} of {Attempts} for SKU {Sku} failed with {FailureKind}",
                attempt, Attempts, sku.Value, exception.Kind);
            throw;
        }
    }
}
