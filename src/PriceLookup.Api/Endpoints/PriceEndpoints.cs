using Microsoft.AspNetCore.Http.HttpResults;
using PriceLookup.Api.Errors;
using PriceLookup.Core.Application.GetProductPrice;

namespace PriceLookup.Api.Endpoints;

public static class PriceEndpoints
{
    public static IEndpointRouteBuilder MapPriceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/products/{sku}/price", GetPriceAsync)
            .WithName("GetProductPrice")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);
        return endpoints;
    }

    private static async Task<Results<Ok<PriceResponse>, ProblemHttpResult>> GetPriceAsync(
        string sku, GetProductPriceHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(sku, cancellationToken);
        return result.IsSuccess
            ? TypedResults.Ok(PriceResponseMapper.ToResponse(result.Price))
            : PriceLookupProblemMapper.ToProblem(result.Error.Value, sku);
    }
}
