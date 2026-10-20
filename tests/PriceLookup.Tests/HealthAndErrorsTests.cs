using System.Net;
using System.Text.Json;
using PriceLookup.Tests.Infrastructure;

namespace PriceLookup.Tests;

public sealed class HealthAndErrorsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_health_returns_200()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_route_returns_404_problem_with_route_not_found()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/nope", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadProblemAsync(response);
        Assert.Equal("route_not_found", problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
        Assert.Equal("GET /nope", problem.GetProperty("instance").GetString());
    }

    [Fact]
    public async Task Post_health_returns_405_problem_with_method_not_allowed()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/health", content: null, Ct);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadProblemAsync(response);
        Assert.Equal("method_not_allowed", problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
    }

    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response)
    {
        await using var body = await response.Content.ReadAsStreamAsync(Ct);
        using var document = await JsonDocument.ParseAsync(body, cancellationToken: Ct);
        return document.RootElement.Clone();
    }
}
