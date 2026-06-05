using System.Text.Json;

namespace Native.Mcp;

/// <summary>
/// Ambient context for a single tool execution. Carries identity (JWT claims already
/// validated by the upstream API Gateway JWT Authorizer), correlation metadata and
/// timing. Instances are created by the runtime per request; tests can build one via
/// <c>McpExecutionContextBuilder</c> in <c>Native.Mcp.Testing</c>.
/// </summary>
/// <remarks>
/// Authorization is <b>role-based</b> (ADR-0006), mirroring <c>NativeLambdaRouter</c>: roles are
/// read from the <c>role</c>, <c>roles</c>, <c>cognito:groups</c> and <c>groups</c> claims, in
/// single, comma-separated and JSON-array forms, case-sensitive.
/// </remarks>
public sealed class McpExecutionContext
{
    private static readonly IReadOnlyDictionary<string, string> EmptyClaims =
        new Dictionary<string, string>(0);

    private static readonly string[] RoleClaimTypes = ["role", "roles", "cognito:groups", "groups"];

    private readonly IReadOnlyList<string> _roles;

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

        _roles = ParseRoles(Claims);
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

    /// <summary>The roles granted to the caller (merged from the role claims, deduplicated).</summary>
    public IReadOnlyList<string> Roles => _roles;

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

    /// <summary>Checks whether the caller has a role (case-sensitive).</summary>
    /// <param name="role">The role to look for.</param>
    /// <returns><see langword="true"/> if the caller has the role.</returns>
    public bool HasRole(string role)
    {
        ArgumentNullException.ThrowIfNull(role);
        for (var i = 0; i < _roles.Count; i++)
        {
            if (string.Equals(_roles[i], role, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Checks whether the caller has at least one of the given roles (case-sensitive).</summary>
    /// <param name="roles">The roles to look for.</param>
    /// <returns><see langword="true"/> if the caller has any of the roles.</returns>
    public bool HasAnyRole(params string[] roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        foreach (var role in roles)
        {
            if (HasRole(role))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Throws <see cref="McpForbiddenException"/> if the caller lacks <paramref name="role"/>.
    /// A convenience guard for tools; the runtime maps the exception to a canonical
    /// <c>forbidden</c> envelope (HTTP 200, <c>isError=true</c>).
    /// </summary>
    /// <param name="role">The required role.</param>
    public void RequireRole(string role)
    {
        if (!HasRole(role))
        {
            throw new McpForbiddenException(role);
        }
    }

    /// <summary>
    /// Checks whether a claim of <paramref name="type"/> carries <paramref name="value"/>
    /// (exact match, or membership in a space/comma-separated value). Case-sensitive.
    /// </summary>
    /// <param name="type">The claim type.</param>
    /// <param name="value">The expected value.</param>
    /// <returns><see langword="true"/> if present.</returns>
    public bool HasClaim(string type, string value)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(value);

        if (!Claims.TryGetValue(type, out var raw) || string.IsNullOrEmpty(raw))
        {
            return false;
        }

        if (string.Equals(raw, value, StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var part in raw.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.Equals(part, value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> ParseRoles(IReadOnlyDictionary<string, string> claims)
    {
        List<string>? roles = null;
        HashSet<string>? seen = null;

        foreach (var claimType in RoleClaimTypes)
        {
            if (!claims.TryGetValue(claimType, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            foreach (var role in ParseClaimValue(raw))
            {
                seen ??= new HashSet<string>(StringComparer.Ordinal);
                if (seen.Add(role))
                {
                    (roles ??= []).Add(role);
                }
            }
        }

        return roles ?? (IReadOnlyList<string>)Array.Empty<string>();
    }

    private static IEnumerable<string> ParseClaimValue(string raw)
    {
        var trimmed = raw.Trim();

        // JSON array form: ["a","b"].
        if (trimmed.StartsWith('['))
        {
            string[]? parsed = TryParseJsonArray(trimmed);
            if (parsed is not null)
            {
                return parsed;
            }
        }

        // Single or comma-separated form.
        return trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string[]? TryParseJsonArray(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var values = new List<string>(document.RootElement.GetArrayLength());
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.String && element.GetString() is { Length: > 0 } value)
                {
                    values.Add(value.Trim());
                }
            }

            return [.. values];
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
