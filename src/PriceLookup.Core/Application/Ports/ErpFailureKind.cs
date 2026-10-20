namespace PriceLookup.Core.Application.Ports;

public enum ErpFailureKind
{
    Transient,
    AttemptTimeout,
    Unauthorized,
    InvalidResponse,
    Timeout,
}
