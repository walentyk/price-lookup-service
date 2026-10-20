using PriceLookup.Core.Application.Ports;

namespace PriceLookup.Core.Application.GetProductPrice;

public static class ErpFailureMapper
{
    public static PriceLookupError Map(ErpFailureKind kind) => kind switch
    {
        ErpFailureKind.Transient => PriceLookupError.ErpUnavailable,
        ErpFailureKind.Unauthorized => PriceLookupError.ErpAuthFailed,
        ErpFailureKind.InvalidResponse => PriceLookupError.ErpInvalidResponse,
        ErpFailureKind.AttemptTimeout or ErpFailureKind.Timeout => PriceLookupError.ErpTimeout,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown ERP failure kind."),
    };
}
