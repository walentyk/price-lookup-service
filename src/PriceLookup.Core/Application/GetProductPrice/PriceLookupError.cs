namespace PriceLookup.Core.Application.GetProductPrice;

public enum PriceLookupError
{
    InvalidSku,
    SkuNotFound,
    ErpUnavailable,
    ErpAuthFailed,
    ErpInvalidResponse,
    ErpTimeout,
}
