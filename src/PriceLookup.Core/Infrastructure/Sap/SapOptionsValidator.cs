using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PriceLookup.Core.Infrastructure.Sap.Security;

namespace PriceLookup.Core.Infrastructure.Sap;

// A8 and A24: the only relaxation is the Development environment (plain HTTP to the stub, certificate optional).
// There is no setting that turns authentication off.
public sealed class SapOptionsValidator(IHostEnvironment environment) : IValidateOptions<SapOptions>
{
    public ValidateOptionsResult Validate(string? name, SapOptions options)
    {
        string?[] checks =
        [
            BaseUrlFailure(options.BaseUrl),
            ServicePathFailure(options.ServicePath),
            SapClientFailure(options.SapClient),
            CertificateFailure(options),
        ];
        var failures = checks.OfType<string>().ToList();
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static string? ServicePathFailure(string servicePath) =>
        string.IsNullOrWhiteSpace(servicePath) ? "Sap:ServicePath must not be empty." : null;

    private static string? SapClientFailure(string sapClient) =>
        sapClient is { Length: 3 } && sapClient.All(char.IsAsciiDigit) ? null : "Sap:SapClient must be exactly three digits.";

    private string? BaseUrlFailure(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return "Sap:BaseUrl must be an absolute URL.";
        }

        var allowed = uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && environment.IsDevelopment());
        return allowed ? null : "Sap:BaseUrl must use https (http is allowed only in Development).";
    }

    // A certificate that is configured must load in every environment, Development included.
    private string? CertificateFailure(SapOptions options)
    {
        if (string.IsNullOrEmpty(options.ClientCertificateBase64))
        {
            return environment.IsDevelopment() ? null : "Sap:ClientCertificateBase64 is required outside Development.";
        }

        try
        {
            using var certificate = ClientCertificateLoader.Load(options.ClientCertificateBase64, options.ClientCertificatePassword);
            return null;
        }
        catch (InvalidOperationException exception)
        {
            return exception.Message;
        }
    }
}
