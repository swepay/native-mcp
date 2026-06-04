using Amazon.Lambda.APIGatewayEvents;

namespace Native.Mcp.ApiGateway;

/// <summary>
/// Builds an API Gateway HTTP API v2 proxy response from a dispatcher result.
/// </summary>
public static class ApiGatewayResponseBuilder
{
    private const string ContentType = "application/json";

    /// <summary>
    /// Wraps a <see cref="McpDispatchResult"/> into an
    /// <see cref="APIGatewayHttpApiV2ProxyResponse"/>. Bodies are returned as-is (never base64).
    /// A bodiless 2xx (e.g. a notification ack) carries no content-type header.
    /// </summary>
    /// <param name="result">The dispatcher result.</param>
    /// <returns>The proxy response.</returns>
    public static APIGatewayHttpApiV2ProxyResponse Build(McpDispatchResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (string.IsNullOrEmpty(result.Body))
        {
            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = result.HttpStatusCode,
                Body = string.Empty,
                Headers = new Dictionary<string, string>(0),
            };
        }

        return new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = result.HttpStatusCode,
            Body = result.Body,
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["content-type"] = ContentType,
            },
        };
    }
}
