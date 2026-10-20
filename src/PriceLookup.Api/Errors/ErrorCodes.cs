namespace PriceLookup.Api.Errors;

public static class ErrorCodes
{
    public const string InvalidSku = "invalid_sku";
    public const string SkuNotFound = "sku_not_found";
    public const string ErpUnavailable = "erp_unavailable";
    public const string ErpAuthFailed = "erp_auth_failed";
    public const string ErpInvalidResponse = "erp_invalid_response";
    public const string ErpTimeout = "erp_timeout";
    public const string RouteNotFound = "route_not_found";
    public const string MethodNotAllowed = "method_not_allowed";
    public const string InternalError = "internal_error";
    public const string HttpError = "http_error";
}
