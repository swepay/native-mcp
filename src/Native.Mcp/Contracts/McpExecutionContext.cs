namespace Native.Mcp;

/// <summary>
/// Ambient context for a single tool execution. Carries identity (JWT claims already
/// validated by the upstream API Gateway JWT Authorizer), correlation metadata and
/// timing. Instances are created by the runtime per request; tests can build one via
/// <c>McpExecutionContextBuilder</c> in <c>Native.Mcp.Testing</c>.
/// </summary>
public sealed class McpExecutionContext
{
    private static readonly IReadOnlyDictionary<string, string> EmptyClaims =
        new Dictionary<string, string>(0);

    private readonly IReadOnlyList<string> _scopes;

    /// <summary>Initializes a new <see cref="McpExecutionContext"/>.</summary>
    /// <param name="toolName">The tool being executed.</param>
    /// <param name="requestId">JSON-RPC request id, echoed in the response.</param>
    /// <param name="correlationId">Cross-system correlation id.</param>
    /// <param name="claims">JWT claims validated upstream (may be empty).</param>
    /// <param name="rawJwt">Raw bearer token for downstream forwarding (never logged).</param>
    /// <param name="startedAt">Invocation start time, for duration measurement.</param>
    /// <param name="idempotencyKey">Optional idempotency key.</param>
    public McpExecutionContext(
        string toolName,
        string requestId,
        string correlationId,
        IReadOnlyDictionary<string, string>? claims,
        string rawJwt,
        DateTimeOffset startedAt,
        string? idempotencyKey = null)
    {
        ArgumentNullException.ThrowIfNull(toolName);
        ArgumentNullException.ThrowIfNull(requestId);
        ArgumentNullException.ThrowIfNull(correlationId);

        ToolName = toolName;
        RequestId = requestId;
        CorrelationId = correlationId;
        Claims = claims ?? EmptyClaims;
        RawJwt = rawJwt ?? string.Empty;
        StartedAt = startedAt;
        IdempotencyKey = idempotencyKey;

        _scopes = ParseScopes(Claims);
    }

    /// <summary>Tool name being executed.</summary>
    public string ToolName { get; }

    /// <summary>Request id from the JSON-RPC envelope (echoed in the response).</summary>
    public string RequestId { get; }

    /// <summary>Correlation id for tracing across systems (from <c>X-Correlation-Id</c>).</summary>
    public string CorrelationId { get; }

    /// <summary>Idempotency key (from <c>X-Idempotency-Key</c>), if provided.</summary>
    public string? IdempotencyKey { get; }

    /// <summary>JWT claims validated by the upstream API Gateway JWT Authorizer.</summary>
    public IReadOnlyDictionary<string, string> Claims { get; }

    /// <summary>The scopes granted to the caller (parsed from <c>scope</c>/<c>scp</c>).</summary>
    public IReadOnlyList<string> Scopes => _scopes;

    /// <summary>The subject (<c>sub</c> claim), or empty if absent.</summary>
    public string Subject => Claims.TryGetValue("sub", out var v) ? v : string.Empty;

    /// <summary>The audience (<c>aud</c> claim), or empty if absent.</summary>
    public string Audience => Claims.TryGetValue("aud", out var v) ? v : string.Empty;

    /// <summary>
    /// The raw JWT, available for forwarding to downstream backends.
    /// IMPORTANT: never log this value.
    /// </summary>
    public string RawJwt { get; }

    /// <summary>Invocation start time (for duration measurement).</summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>Checks whether a scope is present (case-sensitive).</summary>
    /// <param name="scope">The scope to look for.</param>
    /// <returns><see langword="true"/> if the caller has the scope.</returns>
    public bool HasScope(string scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        for (var i = 0; i < _scopes.Count; i++)
        {
            if (string.Equals(_scopes[i], scope, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> ParseScopes(IReadOnlyDictionary<string, string> claims)
    {
        // OAuth2/OIDC carry scopes either in "scope" (space-delimited) or "scp".
        if (!claims.TryGetValue("scope", out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            if (!claims.TryGetValue("scp", out raw) || string.IsNullOrWhiteSpace(raw))
            {
                return Array.Empty<string>();
            }
        }

        return raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
