using System.Diagnostics;

namespace PriceLookup.Api.Errors;

public static class ProblemDetailsSetup
{
    public static IServiceCollection AddPriceLookupProblemDetails(this IServiceCollection services) =>
        services.AddProblemDetails(options => options.CustomizeProblemDetails = Customize);

    // Values set by the code that created the problem (for example a lookup error) win over these defaults.
    private static void Customize(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        var http = context.HttpContext;

        problem.Extensions.TryAdd("code", CodeFor(problem.Status ?? http.Response.StatusCode));
        problem.Instance ??= $"{http.Request.Method} {http.Request.Path}";
        problem.Extensions.TryAdd("traceId", Activity.Current?.Id ?? http.TraceIdentifier);
    }

    private static string CodeFor(int status) => status switch
    {
        StatusCodes.Status404NotFound => ErrorCodes.RouteNotFound,
        StatusCodes.Status405MethodNotAllowed => ErrorCodes.MethodNotAllowed,
        StatusCodes.Status500InternalServerError => ErrorCodes.InternalError,
        _ => ErrorCodes.HttpError,
    };
}
