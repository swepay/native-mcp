using System.Text;
using Amazon.Lambda.APIGatewayEvents;

namespace Native.Mcp.ApiGateway;

/// <summary>
/// Adapts an API Gateway HTTP API v2 proxy event into the transport-agnostic inputs the
/// dispatcher consumes: the decoded JSON-RPC body and an <see cref="McpRequestContext"/>.
/// </summary>
public static class ApiGatewayRequestAdapter
{
    private const string AuthorizationHeader = "authorization";
    private const string CorrelationIdHeader = "x-correlation-id";
    private const string IdempotencyKeyHeader = "x-idempotency-key";
    private const string BearerPrefix = "Bearer ";

    /// <summary>
    /// Adapts the request, decoding a base64 body if needed and extracting claims, the raw
    /// bearer token, the correlation id and the idempotency key.
    /// </summary>
    /// <param name="request">The HTTP API v2 proxy request.</param>
    /// <returns>The decoded body and the request context.</returns>
    public static McpAdaptedRequest Adapt(APIGatewayHttpApiV2ProxyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var headers = BuildHeaderLookup(request.Headers);

        var rawJwt = ExtractBearerToken(headers);
        var correlationId = headers.TryGetValue(CorrelationIdHeader, out var corr) ? corr : string.Empty;
        var idempotencyKey = headers.TryGetValue(IdempotencyKeyHeader, out var idem) && !string.IsNullOrWhiteSpace(idem)
            ? idem
            : null;

        var context = new McpRequestContext
        {
            Claims = ApiGatewayClaimsExtractor.Extract(request),
            RawJwt = rawJwt,
            CorrelationId = correlationId,
            IdempotencyKey = idempotencyKey,
        };

        return new McpAdaptedRequest(DecodeBody(request), context);
    }

    private static Dictionary<string, string> BuildHeaderLookup(IDictionary<string, string>? headers)
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (headers is null)
        {
            return lookup;
        }

        foreach (var header in headers)
        {
            lookup[header.Key] = header.Value;
        }

        return lookup;
    }

    private static string ExtractBearerToken(Dictionary<string, string> headers)
    {
        if (!headers.TryGetValue(AuthorizationHeader, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? value[BearerPrefix.Length..].Trim()
            : value.Trim();
    }

    private static string? DecodeBody(APIGatewayHttpApiV2ProxyRequest request)
    {
        if (string.IsNullOrEmpty(request.Body))
        {
            return request.Body;
        }

        if (!request.IsBase64Encoded)
        {
            return request.Body;
        }

        var bytes = Convert.FromBase64String(request.Body);
        return Encoding.UTF8.GetString(bytes);
    }
}

/// <summary>The output of <see cref="ApiGatewayRequestAdapter.Adapt"/>.</summary>
/// <param name="Body">The decoded JSON-RPC body (may be null/empty).</param>
/// <param name="Context">The request context (claims, correlation, idempotency).</param>
public sealed record McpAdaptedRequest(string? Body, McpRequestContext Context);
