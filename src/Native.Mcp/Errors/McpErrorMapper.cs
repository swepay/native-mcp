namespace Native.Mcp;

/// <summary>
/// Maps unexpected exceptions thrown by a tool into a leak-free canonical error. This is
/// the single place that enforces the LGPD rule: no exception message, stack trace, type
/// name or internal identifier ever reaches the response. Full detail is logged internally
/// by the dispatcher before mapping.
/// </summary>
public static class McpErrorMapper
{
    /// <summary>
    /// Maps any unhandled exception to a generic internal-error problem.
    /// </summary>
    /// <param name="exception">The caught exception (used only for the type check below; never serialized).</param>
    /// <param name="requestId">Correlation id to echo so support can trace internal logs.</param>
    /// <returns>A leak-free <see cref="SwepayProblemDetails"/>.</returns>
    public static SwepayProblemDetails MapException(Exception exception, string requestId)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return McpProblems.InternalError(requestId);
    }
}
