using Amazon.Lambda.APIGatewayEvents;

namespace Native.Mcp.ApiGateway;

/// <summary>
/// Extracts JWT claims from an API Gateway HTTP API v2 event. The claims were validated
/// upstream by the native JWT Authorizer (signature, <c>exp</c>, <c>iss</c>, <c>aud</c>),
/// so this performs no cryptographic verification — only extraction.
/// </summary>
public static class ApiGatewayClaimsExtractor
{
    private static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>(0);

    /// <summary>
    /// Extracts the JWT claims from <paramref name="request"/>, or an empty dictionary if
    /// no authorizer claims are present.
    /// </summary>
    /// <param name="request">The HTTP API v2 proxy request.</param>
    /// <returns>The claims (read-only).</returns>
    public static IReadOnlyDictionary<string, string> Extract(APIGatewayHttpApiV2ProxyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var jwtClaims = request.RequestContext?.Authorizer?.Jwt?.Claims;
        if (jwtClaims is null || jwtClaims.Count == 0)
        {
            return Empty;
        }

        var claims = new Dictionary<string, string>(jwtClaims.Count, StringComparer.Ordinal);
        foreach (var claim in jwtClaims)
        {
            claims[claim.Key] = claim.Value ?? string.Empty;
        }

        return claims;
    }
}
