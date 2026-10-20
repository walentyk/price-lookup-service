using PriceLookup.Core.Domain;

namespace PriceLookup.Core.Application.Ports;

/// <summary>
/// The port to the ERP system that owns product prices.
/// </summary>
public interface IErpClient
{
    /// <summary>
    /// Performs one logical price lookup for <paramref name="sku"/>.
    /// </summary>
    /// <param name="sku">A valid, normalised SKU.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The current price, or <c>null</c> when the SKU does not exist in the ERP.</returns>
    /// <exception cref="ErpException">The ERP could not deliver a price; <see cref="ErpException.Kind"/> says why.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled; it is let through and never wrapped in an <see cref="ErpException"/>.
    /// </exception>
    Task<ProductPrice?> GetPriceAsync(Sku sku, CancellationToken cancellationToken);
}
