namespace Native.Mcp;

/// <summary>
/// Factory for the common <see cref="SwepayProblemDetails"/> the runtime emits. Keeps the
/// canonical type URIs, titles, codes and recovery copy in one place. None of these ever
/// embed exception text, JWTs or claims (LGPD).
/// </summary>
public static class McpProblems
{
    /// <summary>Builds a validation-failed problem (HTTP 400).</summary>
    /// <param name="detail">End-user safe description of what failed.</param>
    /// <param name="requestId">Correlation id to echo.</param>
    /// <param name="instance">Optional occurrence reference.</param>
    /// <returns>A populated problem.</returns>
    public static SwepayProblemDetails ValidationFailed(string detail, string requestId, string? instance = null) =>
        new(
            Type: ProblemTypes.ValidationFailed,
            Title: "Validation Failed",
            Status: 400,
            Detail: detail,
            Instance: instance,
            Code: "VALIDATION_FAILED",
            Recovery: "Fix the highlighted fields and retry the request.",
            RequestId: requestId);

    /// <summary>Builds a forbidden problem (HTTP 403).</summary>
    /// <param name="detail">End-user safe description (e.g. the required scope).</param>
    /// <param name="requestId">Correlation id to echo.</param>
    /// <returns>A populated problem.</returns>
    public static SwepayProblemDetails Forbidden(string detail, string requestId) =>
        new(
            Type: ProblemTypes.Forbidden,
            Title: "Forbidden",
            Status: 403,
            Detail: detail,
            Instance: null,
            Code: "FORBIDDEN",
            Recovery: "Request the required permission/scope and try again.",
            RequestId: requestId);

    /// <summary>Builds a generic internal-error problem (HTTP 500). Leaks nothing.</summary>
    /// <param name="requestId">Correlation id to echo for support to trace logs.</param>
    /// <returns>A populated problem.</returns>
    public static SwepayProblemDetails InternalError(string requestId) =>
        new(
            Type: ProblemTypes.InternalError,
            Title: "Internal Error",
            Status: 500,
            Detail: "An internal error occurred. See server logs for details.",
            Instance: null,
            Code: "INTERNAL_ERROR",
            Recovery: "Retry later. If the problem persists, contact support with the request id.",
            RequestId: requestId);
}
