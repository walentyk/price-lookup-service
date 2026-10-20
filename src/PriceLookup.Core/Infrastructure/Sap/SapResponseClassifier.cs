using System.Net;
using PriceLookup.Core.Application.Ports;

namespace PriceLookup.Core.Infrastructure.Sap;

public static class SapResponseClassifier
{
    // A14: only 200 carries a price and only 404 means "no such SKU"; every other status is a failure.
    public static SapResponseOutcome Classify(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.OK => SapResponseOutcome.Price,
        HttpStatusCode.NotFound => SapResponseOutcome.NotFound,
        _ => throw new ErpException(ToFailureKind(statusCode), $"SAP answered HTTP {(int)statusCode}."),
    };

    private static ErpFailureKind ToFailureKind(HttpStatusCode statusCode) => (int)statusCode switch
    {
        401 or 403 => ErpFailureKind.Unauthorized,
        408 or 429 or (>= 500 and <= 599) => ErpFailureKind.Transient,
        _ => ErpFailureKind.InvalidResponse,
    };
}

public enum SapResponseOutcome
{
    Price,
    NotFound,
}
