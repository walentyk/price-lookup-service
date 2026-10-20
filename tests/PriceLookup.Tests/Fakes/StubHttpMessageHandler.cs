using System.Net;
using System.Text;

namespace PriceLookup.Tests.Fakes;

// Answers every request with the same scripted response, or throws the scripted exception, and records the requests.
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode statusCode;
    private readonly string body;
    private readonly Exception? exception;
    private readonly List<HttpRequestMessage> requests = [];

    private StubHttpMessageHandler(HttpStatusCode statusCode, string body, Exception? exception)
    {
        this.statusCode = statusCode;
        this.body = body;
        this.exception = exception;
    }

    public IReadOnlyList<HttpRequestMessage> Requests => requests;

    public static StubHttpMessageHandler Returning(HttpStatusCode statusCode, string body = "") =>
        new(statusCode, body, null);

    public static StubHttpMessageHandler Throwing(Exception exception) =>
        new(HttpStatusCode.OK, "", exception);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        requests.Add(request);
        if (exception is not null)
        {
            return Task.FromException<HttpResponseMessage>(exception);
        }

        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
            RequestMessage = request,
        };
        return Task.FromResult(response);
    }
}
