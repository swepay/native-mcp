using NativeLambdaRouter;

namespace Native.Mcp.NativeLambdaRouter;

/// <summary>
/// Maps a <see cref="RouteContext"/> (produced by NativeLambdaRouter, which already extracted
/// the JWT claims) into the transport-agnostic <see cref="McpRequestContext"/> the dispatcher
/// consumes.
/// </summary>
public static class McpRouteContextMapper
{
    private const string AuthorizationHeader = "authorization";
    private const string BearerPrefix = "Bearer ";
    private const string CorrelationIdHeader = "x-correlation-id";
    private const string IdempotencyKeyHeader = "x-idempotency-key";

    /// <summary>Builds an <see cref="McpRequestContext"/> from a router <see cref="RouteContext"/>.</summary>
    /// <param name="context">The router request context.</param>
    /// <returns>The MCP request context (claims, raw JWT, correlation, idempotency).</returns>
    public static McpRequestContext ToRequestContext(RouteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var headers = context.Headers;
        return new McpRequestContext
        {
            Claims = context.Claims,
            RawJwt = ExtractBearer(headers),
            CorrelationId = headers.TryGetValue(CorrelationIdHeader, out var correlation) ? correlation : string.Empty,
            IdempotencyKey = headers.TryGetValue(IdempotencyKeyHeader, out var idempotency)
                && !string.IsNullOrWhiteSpace(idempotency)
                ? idempotency
                : null,
        };
    }

    private static string ExtractBearer(IDictionary<string, string> headers)
    {
        if (!headers.TryGetValue(AuthorizationHeader, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? value[BearerPrefix.Length..].Trim()
            : value.Trim();
    }
}
