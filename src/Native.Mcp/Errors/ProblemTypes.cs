namespace Native.Mcp;

/// <summary>
/// Canonical Swepay problem-type URIs commonly emitted by the MCP runtime.
/// These resolve to documentation pages on the Swepay error catalog
/// (<c>https://errors.swepay.com.br</c>).
/// </summary>
public static class ProblemTypes
{
    /// <summary>Base URI for common, cross-product error types.</summary>
    public const string CommonBase = "https://errors.swepay.com.br/common/";

    /// <summary>Input failed validation (RFC 9457, HTTP 400).</summary>
    public const string ValidationFailed = CommonBase + "validation-failed";

    /// <summary>Caller lacks a required scope/permission (HTTP 403).</summary>
    public const string Forbidden = CommonBase + "forbidden";

    /// <summary>An unexpected server-side error occurred (HTTP 500).</summary>
    public const string InternalError = CommonBase + "internal-error";

    /// <summary>The requested resource was not found (HTTP 404).</summary>
    public const string NotFound = CommonBase + "not-found";

    /// <summary>The request conflicts with current state (HTTP 409).</summary>
    public const string Conflict = CommonBase + "conflict";
}
