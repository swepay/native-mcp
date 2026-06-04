namespace Native.Mcp;

/// <summary>
/// Transport-agnostic per-request context handed to the dispatcher. The API Gateway
/// adapter builds this from the HTTP API v2 event; tests can build it directly.
/// </summary>
public sealed class McpRequestContext
{
    private static readonly IReadOnlyDictionary<string, string> EmptyClaims =
        new Dictionary<string, string>(0);

    /// <summary>JWT claims validated by the upstream API Gateway JWT Authorizer.</summary>
    public IReadOnlyDictionary<string, string> Claims { get; init; } = EmptyClaims;

    /// <summary>The raw bearer token, for downstream forwarding. Never logged.</summary>
    public string RawJwt { get; init; } = string.Empty;

    /// <summary>Correlation id from <c>X-Correlation-Id</c>. If empty, one is minted.</summary>
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>Idempotency key from <c>X-Idempotency-Key</c>, if present.</summary>
    public string? IdempotencyKey { get; init; }
}

/// <summary>The result of dispatching a request: an HTTP status and a JSON body.</summary>
/// <param name="HttpStatusCode">HTTP status to return (200 for valid JSON-RPC, 202 for notifications).</param>
/// <param name="Body">The JSON-RPC response body (empty for notifications).</param>
public sealed record McpDispatchResult(int HttpStatusCode, string Body);
