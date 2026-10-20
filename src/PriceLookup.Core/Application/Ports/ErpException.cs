namespace PriceLookup.Core.Application.Ports;

public sealed class ErpException(ErpFailureKind kind, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public ErpFailureKind Kind { get; } = kind;

    public bool IsRetryable => Kind is ErpFailureKind.Transient or ErpFailureKind.AttemptTimeout;
}
