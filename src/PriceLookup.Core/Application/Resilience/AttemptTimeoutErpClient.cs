using PriceLookup.Core.Application.Ports;
using PriceLookup.Core.Domain;

namespace PriceLookup.Core.Application.Resilience;

public sealed class AttemptTimeoutErpClient(IErpClient inner, ErpResilienceOptions options) : IErpClient
{
    // A hang becomes a retryable AttemptTimeout only when this attempt's own timer fired.
    // If the incoming token fired (total timeout or caller), the cancellation is passed through.
    public async Task<ProductPrice?> GetPriceAsync(Sku sku, CancellationToken cancellationToken)
    {
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        attempt.CancelAfter(options.AttemptTimeoutMs);
        try
        {
            return await inner.GetPriceAsync(sku, attempt.Token);
        }
        catch (OperationCanceledException exception)
            when (attempt.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new ErpException(
                ErpFailureKind.AttemptTimeout,
                $"ERP attempt for SKU '{sku}' did not finish within {options.AttemptTimeoutMs} ms.",
                exception);
        }
    }
}
