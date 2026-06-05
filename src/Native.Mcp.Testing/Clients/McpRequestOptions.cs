namespace Native.Mcp.Testing;

/// <summary>
/// Per-call options for <see cref="McpTestClient"/>: identity (scopes/claims/JWT) and
/// correlation metadata. Mirrors what the API Gateway adapter would extract from a real event.
/// </summary>
public sealed class McpRequestOptions
{
    /// <summary>Correlation id (defaults to a generated one if null).</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Idempotency key.</summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>Roles to grant (rendered into the <c>roles</c> claim).</summary>
    public IReadOnlyList<string>? Roles { get; set; }

    /// <summary>Additional raw JWT claims.</summary>
    public IReadOnlyDictionary<string, string>? Claims { get; set; }

    /// <summary>Raw bearer token (made available to tools via <see cref="McpExecutionContext.RawJwt"/>).</summary>
    public string? RawJwt { get; set; }

    /// <summary>Explicit JSON-RPC request id (string). Defaults to an auto-incrementing value.</summary>
    public string? Id { get; set; }

    internal McpRequestContext ToRequestContext()
    {
        var claims = new Dictionary<string, string>(StringComparer.Ordinal);
        if (Claims is not null)
        {
            foreach (var claim in Claims)
            {
                claims[claim.Key] = claim.Value;
            }
        }

        if (Roles is { Count: > 0 })
        {
            claims["roles"] = string.Join(",", Roles);
        }

        return new McpRequestContext
        {
            Claims = claims,
            RawJwt = RawJwt ?? string.Empty,
            CorrelationId = CorrelationId ?? string.Empty,
            IdempotencyKey = IdempotencyKey,
        };
    }
}
