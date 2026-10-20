using System.Text;
using PriceLookup.Core.Infrastructure.Sap.Security;

namespace PriceLookup.Tests.Infrastructure.Sap;

public sealed class ClientCertificateLoaderTests
{
    private const string Password = "s3cret-pfx-password";

    [Theory]
    [InlineData(Password)]
    [InlineData(null)]
    public void Base64_pfx_with_private_key_loads(string? password)
    {
        using var certificate = ClientCertificateLoader.Load(TestCertificates.CreateBase64Pfx(password), password);

        Assert.True(certificate.HasPrivateKey);
        Assert.Equal(TestCertificates.Subject, certificate.Subject);
    }

    [Fact]
    public void Garbage_base64_fails_with_a_clear_message()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => ClientCertificateLoader.Load("not base64 !!", null));

        Assert.Equal("Sap:ClientCertificateBase64 is not valid base64.", exception.Message);
    }

    [Fact]
    public void Base64_that_is_not_a_pfx_fails()
    {
        var notAPfx = Convert.ToBase64String(Encoding.UTF8.GetBytes("hello"));

        var exception = Assert.Throws<InvalidOperationException>(() => ClientCertificateLoader.Load(notAPfx, null));

        Assert.Contains("could not be read as a PFX", exception.Message);
    }

    [Fact]
    public void Certificate_without_private_key_fails()
    {
        var pfx = TestCertificates.CreateBase64PfxWithoutPrivateKey();

        var exception = Assert.Throws<InvalidOperationException>(() => ClientCertificateLoader.Load(pfx, null));

        Assert.Contains("has no private key", exception.Message);
    }

    [Fact]
    public void Wrong_password_fails_without_revealing_either_password()
    {
        var pfx = TestCertificates.CreateBase64Pfx(Password);
        const string wrongPassword = "wrong-pfx-password";

        var exception = Assert.Throws<InvalidOperationException>(() => ClientCertificateLoader.Load(pfx, wrongPassword));

        Assert.Contains("could not be read as a PFX", exception.Message);
        Assert.DoesNotContain(wrongPassword, exception.ToString());
        Assert.DoesNotContain(Password, exception.ToString());
        Assert.DoesNotContain(pfx, exception.ToString());
    }
}
