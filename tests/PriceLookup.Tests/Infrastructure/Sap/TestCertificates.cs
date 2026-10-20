using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace PriceLookup.Tests.Infrastructure.Sap;

// Self-signed certificates created in memory, so no certificate file or secret is ever committed.
public static class TestCertificates
{
    public const string Subject = "CN=price-lookup-test";

    public static X509Certificate2 CreateSelfSigned()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(Subject, key, HashAlgorithmName.SHA256);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(10));
    }

    public static string CreateBase64Pfx(string? password = null)
    {
        using var certificate = CreateSelfSigned();
        return Convert.ToBase64String(certificate.Export(X509ContentType.Pkcs12, password));
    }

    public static string CreateBase64PfxWithoutPrivateKey()
    {
        using var certificate = CreateSelfSigned();
        using var publicOnly = X509CertificateLoader.LoadCertificate(certificate.RawData);
        return Convert.ToBase64String(publicOnly.Export(X509ContentType.Pkcs12));
    }
}
