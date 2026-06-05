namespace Native.Mcp.Testing;

/// <summary>
/// Fluent builder for <see cref="McpExecutionContext"/> in tests. Defaults are sensible so a
/// bare <c>new McpExecutionContextBuilder().Build()</c> yields a usable context.
/// </summary>
public sealed class McpExecutionContextBuilder
{
    private readonly Dictionary<string, string> _claims = new(StringComparer.Ordinal);
    private readonly List<string> _roles = new();
    private string _toolName = "test_tool";
    private string _requestId = "test-request";
    private string _correlationId = "test-correlation";
    private string _rawJwt = string.Empty;
    private string? _idempotencyKey;
    private DateTimeOffset _startedAt = DateTimeOffset.UtcNow;

    /// <summary>Sets the tool name.</summary>
    /// <param name="toolName">The tool name.</param>
    /// <returns>This builder.</returns>
    public McpExecutionContextBuilder WithToolName(string toolName)
    {
        _toolName = toolName;
        return this;
    }

    /// <summary>Sets the JSON-RPC request id.</summary>
    /// <param name="requestId">The request id.</param>
    /// <returns>This builder.</returns>
    public McpExecutionContextBuilder WithRequestId(string requestId)
    {
        _requestId = requestId;
        return this;
    }

    /// <summary>Sets the correlation id.</summary>
    /// <param name="correlationId">The correlation id.</param>
    /// <returns>This builder.</returns>
    public McpExecutionContextBuilder WithCorrelationId(string correlationId)
    {
        _correlationId = correlationId;
        return this;
    }

    /// <summary>Sets the idempotency key.</summary>
    /// <param name="idempotencyKey">The idempotency key.</param>
    /// <returns>This builder.</returns>
    public McpExecutionContextBuilder WithIdempotencyKey(string? idempotencyKey)
    {
        _idempotencyKey = idempotencyKey;
        return this;
    }

    /// <summary>Sets the raw JWT.</summary>
    /// <param name="rawJwt">The raw bearer token.</param>
    /// <returns>This builder.</returns>
    public McpExecutionContextBuilder WithRawJwt(string rawJwt)
    {
        _rawJwt = rawJwt;
        return this;
    }

    /// <summary>Sets the invocation start time.</summary>
    /// <param name="startedAt">The start time.</param>
    /// <returns>This builder.</returns>
    public McpExecutionContextBuilder WithStartedAt(DateTimeOffset startedAt)
    {
        _startedAt = startedAt;
        return this;
    }

    /// <summary>Adds (or overwrites) a claim.</summary>
    /// <param name="type">The claim type.</param>
    /// <param name="value">The claim value.</param>
    /// <returns>This builder.</returns>
    public McpExecutionContextBuilder WithClaim(string type, string value)
    {
        _claims[type] = value;
        return this;
    }

    /// <summary>Sets the subject (<c>sub</c>) claim.</summary>
    /// <param name="subject">The subject.</param>
    /// <returns>This builder.</returns>
    public McpExecutionContextBuilder WithSubject(string subject) => WithClaim("sub", subject);

    /// <summary>Sets the audience (<c>aud</c>) claim.</summary>
    /// <param name="audience">The audience.</param>
    /// <returns>This builder.</returns>
    public McpExecutionContextBuilder WithAudience(string audience) => WithClaim("aud", audience);

    /// <summary>Grants a role (rendered into the <c>roles</c> claim).</summary>
    /// <param name="role">The role to grant.</param>
    /// <returns>This builder.</returns>
    public McpExecutionContextBuilder WithRole(string role)
    {
        if (!_roles.Contains(role))
        {
            _roles.Add(role);
        }

        return this;
    }

    /// <summary>Ensures a role is NOT granted.</summary>
    /// <param name="role">The role to remove.</param>
    /// <returns>This builder.</returns>
    public McpExecutionContextBuilder WithoutRole(string role)
    {
        _roles.Remove(role);
        return this;
    }

    /// <summary>Builds the <see cref="McpExecutionContext"/>.</summary>
    /// <returns>The constructed context.</returns>
    public McpExecutionContext Build()
    {
        if (_roles.Count > 0)
        {
            _claims["roles"] = string.Join(",", _roles);
        }

        return new McpExecutionContext(
            toolName: _toolName,
            requestId: _requestId,
            correlationId: _correlationId,
            claims: new Dictionary<string, string>(_claims, StringComparer.Ordinal),
            rawJwt: _rawJwt,
            startedAt: _startedAt,
            idempotencyKey: _idempotencyKey);
    }
}
