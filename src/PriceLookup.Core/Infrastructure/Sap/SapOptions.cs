namespace PriceLookup.Core.Infrastructure.Sap;

/// <summary>
/// Connection to the SAP OData price service, bound from the configuration section <c>Sap</c>.
/// </summary>
public sealed class SapOptions
{
    public const string SectionName = "Sap";

    /// <summary>
    /// Scheme, host and port of the SAP system, for example <c>https://sap.example.invalid</c>.
    /// </summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>
    /// Path of the OData V2 service, with a leading and a trailing slash.
    /// </summary>
    public string ServicePath { get; set; } = "/sap/opu/odata/sap/ZSD_PRODUCT_PRICE_SRV/";

    /// <summary>
    /// SAP client (mandant) sent as the <c>sap-client</c> query parameter.
    /// </summary>
    public string SapClient { get; set; } = "100";

    /// <summary>
    /// Base64-encoded PFX of the X.509 client certificate. A secret: never logged.
    /// </summary>
    public string ClientCertificateBase64 { get; set; } = "";

    /// <summary>
    /// Password of the PFX, empty when it has none. A secret: never logged.
    /// </summary>
    public string ClientCertificatePassword { get; set; } = "";
}
