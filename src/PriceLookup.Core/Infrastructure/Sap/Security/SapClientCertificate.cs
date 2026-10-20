using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;

namespace PriceLookup.Core.Infrastructure.Sap.Security;

// A singleton: the certificate is loaded once per process (A23) and shared by every handler the HttpClient
// factory creates, so handler rotation never reloads it. SapOptionsValidator has already checked that it loads.
public sealed class SapClientCertificate(IOptions<SapOptions> options) : IDisposable
{
    public X509Certificate2? Certificate { get; } = Load(options.Value);

    public void Dispose() => Certificate?.Dispose();

    private static X509Certificate2? Load(SapOptions options) =>
        string.IsNullOrEmpty(options.ClientCertificateBase64)
            ? null
            : ClientCertificateLoader.Load(options.ClientCertificateBase64, options.ClientCertificatePassword);
}
