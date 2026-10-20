using PriceLookup.Core.Application.Ports;
using PriceLookup.Core.Domain;

namespace PriceLookup.Core.Application.Resilience;

public sealed class TimeoutErpClient(IErpClient inner, ErpResilienceOptions options) : IErpClient
{
    // The total budget covers every attempt and every pause. A cancelled caller is not an ERP timeout (A18),
    // so the cancellation is passed through when the caller's own token fired.
    public async Task<ProductPrice?> GetPriceAsync(Sku sku, CancellationToken cancellationToken)
    {
        using var total = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        total.CancelAfter(options.TotalTimeoutMs);
        try
        {
            return await inner.GetPriceAsync(sku, total.Token);
        }
        catch (OperationCanceledException exception)
            when (total.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new ErpException(
                ErpFailureKind.Timeout,
                $"ERP lookup for SKU '{sku}' did not finish within {options.TotalTimeoutMs} ms.",
                exception);
        }
    }
}
