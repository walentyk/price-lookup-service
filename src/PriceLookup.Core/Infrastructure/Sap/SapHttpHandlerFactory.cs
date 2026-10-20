using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace PriceLookup.Core.Infrastructure.Sap;

public static class SapHttpHandlerFactory
{
    // A6: TLS 1.2 is the minimum. A41: no RemoteCertificateValidationCallback, so the SAP server certificate
    // is validated with the platform's default rules.
    public static SocketsHttpHandler Create(X509Certificate2? clientCertificate = null)
    {
        var handler = new SocketsHttpHandler();
        handler.SslOptions.EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
        handler.SslOptions.ClientCertificates = new X509CertificateCollection();
        if (clientCertificate is not null)
        {
            handler.SslOptions.ClientCertificates.Add(clientCertificate);
        }

        return handler;
    }
}
