using Microsoft.AspNetCore.Http.HttpResults;
using PriceLookup.Core.Application.GetProductPrice;

namespace PriceLookup.Api.Errors;

// instance, traceId and type are added by ProblemDetailsSetup when the result is written.
public static class PriceLookupProblemMapper
{
    public static ProblemHttpResult ToProblem(PriceLookupError error, string? sku)
    {
        var (status, code, title, detail) = Describe(error, sku);
        var extensions = new Dictionary<string, object?> { ["code"] = code };
        return TypedResults.Problem(detail, statusCode: status, title: title, extensions: extensions);
    }

    private static (int Status, string Code, string Title, string Detail) Describe(PriceLookupError error, string? sku) => error switch
    {
        PriceLookupError.InvalidSku => (StatusCodes.Status400BadRequest, ErrorCodes.InvalidSku, "Invalid SKU",
            $"'{sku}' is not a valid SKU: use 1 to 40 letters, digits, '.', '_' or '-', starting with a letter or digit."),
        PriceLookupError.SkuNotFound => (StatusCodes.Status404NotFound, ErrorCodes.SkuNotFound, "SKU not found",
            $"The ERP has no product with SKU '{sku}'."),
        PriceLookupError.ErpUnavailable => (StatusCodes.Status502BadGateway, ErrorCodes.ErpUnavailable, "ERP unavailable",
            $"The ERP could not be reached to look up SKU '{sku}'."),
        PriceLookupError.ErpAuthFailed => (StatusCodes.Status502BadGateway, ErrorCodes.ErpAuthFailed, "ERP authentication failed",
            $"The ERP rejected the service's credentials while looking up SKU '{sku}'."),
        PriceLookupError.ErpInvalidResponse => (StatusCodes.Status502BadGateway, ErrorCodes.ErpInvalidResponse, "ERP returned an invalid response",
            $"The ERP answer for SKU '{sku}' could not be read."),
        PriceLookupError.ErpTimeout => (StatusCodes.Status504GatewayTimeout, ErrorCodes.ErpTimeout, "ERP timeout",
            $"The ERP did not return a price for SKU '{sku}' in time."),
        _ => throw new ArgumentOutOfRangeException(nameof(error), error, "Unknown price lookup error."),
    };
}
