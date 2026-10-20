using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using PriceLookup.Core.Infrastructure.Sap;

namespace PriceLookup.Tests.Infrastructure.Sap;

public sealed class SapHttpHandlerFactoryTests
{
    [Fact]
    public void Only_tls_1_2_and_1_3_are_enabled()
    {
        using var handler = SapHttpHandlerFactory.Create();

        Assert.Equal(SslProtocols.Tls12 | SslProtocols.Tls13, handler.SslOptions.EnabledSslProtocols);
    }

    [Fact]
    public void Server_certificate_is_validated_with_platform_defaults()
    {
        using var handler = SapHttpHandlerFactory.Create();

        Assert.Null(handler.SslOptions.RemoteCertificateValidationCallback);
    }

    [Fact]
    public void Client_certificate_passed_in_is_attached()
    {
        using var certificate = CreateSelfSignedCertificate();

        using var handler = SapHttpHandlerFactory.Create(certificate);

        var attached = Assert.Single(handler.SslOptions.ClientCertificates!.Cast<X509Certificate>());
        Assert.Same(certificate, attached);
    }

    [Fact]
    public void Without_a_certificate_no_client_certificate_is_attached()
    {
        using var handler = SapHttpHandlerFactory.Create();

        Assert.NotNull(handler.SslOptions.ClientCertificates);
        Assert.Empty(handler.SslOptions.ClientCertificates);
    }

    private static X509Certificate2 CreateSelfSignedCertificate()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=price-lookup-test", key, HashAlgorithmName.SHA256);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(10));
    }
}
