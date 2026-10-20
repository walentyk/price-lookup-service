using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace PriceLookup.Core.Infrastructure.Sap.Security;

// A23: the certificate arrives as a base64 PFX. Error messages name the setting, never its value or the password.
public static class ClientCertificateLoader
{
    public static X509Certificate2 Load(string base64Pfx, string? password)
    {
        var certificate = LoadPkcs12(DecodeBase64(base64Pfx), password);
        if (certificate.HasPrivateKey)
        {
            return certificate;
        }

        certificate.Dispose();
        throw new InvalidOperationException(
            "The certificate in Sap:ClientCertificateBase64 has no private key; mutual TLS needs a PFX that contains it.");
    }

    private static byte[] DecodeBase64(string base64Pfx)
    {
        try
        {
            return Convert.FromBase64String(base64Pfx);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Sap:ClientCertificateBase64 is not valid base64.");
        }
    }

    // The inner exception comes from the platform's PKCS#12 reader and carries no secret values.
    private static X509Certificate2 LoadPkcs12(byte[] pfx, string? password)
    {
        try
        {
            return X509CertificateLoader.LoadPkcs12(pfx, string.IsNullOrEmpty(password) ? null : password);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException(
                "Sap:ClientCertificateBase64 could not be read as a PFX with Sap:ClientCertificatePassword "
                + "(damaged data or wrong password).",
                exception);
        }
    }
}
