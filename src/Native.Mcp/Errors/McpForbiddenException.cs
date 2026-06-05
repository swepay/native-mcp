namespace Native.Mcp;

/// <summary>
/// Thrown by <see cref="McpExecutionContext.RequireRole"/> when the caller lacks a required
/// role. The dispatcher catches it and maps it to the canonical <c>forbidden</c> envelope
/// (HTTP 200, <c>isError=true</c>) — it is never surfaced as an unhandled/internal error.
/// </summary>
public sealed class McpForbiddenException : Exception
{
    /// <summary>Initializes the exception for a missing required role.</summary>
    /// <param name="requiredRole">The role the caller was required to have.</param>
    public McpForbiddenException(string requiredRole)
        : base($"Required role: {requiredRole}")
    {
        RequiredRole = requiredRole;
    }

    /// <summary>The role that was required but absent.</summary>
    public string RequiredRole { get; }
}
