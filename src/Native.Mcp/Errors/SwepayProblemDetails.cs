namespace Native.Mcp;

/// <summary>
/// Canonical Swepay error payload. Superset of RFC 9457
/// (<c>application/problem+json</c>), adding a machine-readable <see cref="Code"/>,
/// UX-written <see cref="Recovery"/> and trace-enabling <see cref="RequestId"/>.
/// </summary>
/// <remarks>
/// <para>This is the <see cref="Native.Mcp"/>-local copy of the canonical Swepay
/// problem-details contract (the same shape is published by <c>Native.OpenApi</c>).
/// It is duplicated here deliberately so the MCP runtime stays lean and does not
/// take a dependency on the OpenAPI library.</para>
/// <para>Do <b>not</b> include stack traces, SQL errors, table names, raw exception
/// messages, JWTs or internal IDs in any field — see LGPD constraints in the
/// Native.Mcp error catalog (README).</para>
/// </remarks>
/// <param name="Type">Canonical error documentation URL, e.g.
/// <c>https://errors.swepay.com.br/common/validation-failed</c>.</param>
/// <param name="Title">Short, human-readable classification (e.g. <c>"Validation Failed"</c>).</param>
/// <param name="Status">HTTP status code as an integer.</param>
/// <param name="Detail">End-user safe message describing the problem.</param>
/// <param name="Instance">URI reference identifying the specific occurrence
/// (typically a resource path or correlation URN). Optional.</param>
/// <param name="Code">Machine-readable code (e.g. <c>VALIDATION_FAILED</c>).</param>
/// <param name="Recovery">Concrete next step the caller should take.</param>
/// <param name="RequestId">Correlation id echoed from the request (or minted server-side).</param>
public sealed record SwepayProblemDetails(
    string Type,
    string Title,
    int Status,
    string Detail,
    string? Instance,
    string Code,
    string Recovery,
    string RequestId)
{
    /// <summary>
    /// Creates a <see cref="SwepayProblemDetails"/> with the common optional fields
    /// defaulted. Useful for tools that do not need to populate every field.
    /// </summary>
    /// <param name="type">Canonical error documentation URL.</param>
    /// <param name="title">Short, human-readable classification.</param>
    /// <param name="status">HTTP status code.</param>
    /// <param name="detail">End-user safe message.</param>
    /// <param name="code">Machine-readable code. Defaults to a slug derived from <paramref name="type"/>.</param>
    /// <param name="recovery">Concrete next step. Defaults to a generic message.</param>
    /// <param name="requestId">Correlation id. Defaults to empty (filled in by the error mapper).</param>
    /// <param name="instance">Occurrence URI reference.</param>
    /// <returns>A populated <see cref="SwepayProblemDetails"/>.</returns>
    public static SwepayProblemDetails Create(
        string type,
        string title,
        int status,
        string detail,
        string? code = null,
        string? recovery = null,
        string? requestId = null,
        string? instance = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);

        return new SwepayProblemDetails(
            Type: type,
            Title: title,
            Status: status,
            Detail: detail,
            Instance: instance,
            Code: code ?? DeriveCode(type),
            Recovery: recovery ?? "Review the request and try again. If the problem persists, contact support.",
            RequestId: requestId ?? string.Empty);
    }

    /// <summary>
    /// Derives an UPPER_SNAKE_CASE code from the last segment of a problem-type URI.
    /// </summary>
    /// <param name="type">The problem-type URI.</param>
    /// <returns>A machine-readable code such as <c>VALIDATION_FAILED</c>.</returns>
    internal static string DeriveCode(string type)
    {
        var lastSegment = type;
        var slash = type.LastIndexOf('/');
        if (slash >= 0 && slash < type.Length - 1)
        {
            lastSegment = type[(slash + 1)..];
        }

        return lastSegment.Replace('-', '_').ToUpperInvariant();
    }
}
