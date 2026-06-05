using System.Text;
using Amazon.Lambda.APIGatewayEvents;
using Shouldly;
using Native.Mcp.ApiGateway;

namespace Native.Mcp.Tests.ApiGateway;

public sealed class ApiGatewayTests
{
    private static APIGatewayHttpApiV2ProxyRequest RequestWith(
        IDictionary<string, string>? headers = null,
        IDictionary<string, string>? claims = null,
        string? body = null,
        bool base64 = false)
    {
        return new APIGatewayHttpApiV2ProxyRequest
        {
            Headers = headers,
            Body = body,
            IsBase64Encoded = base64,
            RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
            {
                Authorizer = claims is null
                    ? null
                    : new APIGatewayHttpApiV2ProxyRequest.AuthorizerDescription
                    {
                        Jwt = new APIGatewayHttpApiV2ProxyRequest.AuthorizerDescription.JwtDescription
                        {
                            Claims = claims,
                        },
                    },
            },
        };
    }

    [Fact]
    public void ClaimsExtractor_ExtractsJwtClaims()
    {
        var request = RequestWith(claims: new Dictionary<string, string> { ["sub"] = "u1", ["scope"] = "a b" });

        var claims = ApiGatewayClaimsExtractor.Extract(request);

        claims["sub"].ShouldBe("u1");
        claims["scope"].ShouldBe("a b");
    }

    [Fact]
    public void ClaimsExtractor_WhenNoAuthorizer_ReturnsEmpty()
    {
        ApiGatewayClaimsExtractor.Extract(RequestWith()).ShouldBeEmpty();
    }

    [Fact]
    public void RequestAdapter_ExtractsBearerTokenAndHeaders()
    {
        var request = RequestWith(
            headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Authorization"] = "Bearer eyJ-token",
                ["x-correlation-id"] = "corr-9",
                ["x-idempotency-key"] = "idem-9",
            },
            body: """{"jsonrpc":"2.0"}""");

        var adapted = ApiGatewayRequestAdapter.Adapt(request);

        adapted.Body.ShouldBe("""{"jsonrpc":"2.0"}""");
        adapted.Context.RawJwt.ShouldBe("eyJ-token");
        adapted.Context.CorrelationId.ShouldBe("corr-9");
        adapted.Context.IdempotencyKey.ShouldBe("idem-9");
    }

    [Fact]
    public void RequestAdapter_DecodesBase64Body()
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"jsonrpc":"2.0"}"""));
        var request = RequestWith(body: encoded, base64: true);

        ApiGatewayRequestAdapter.Adapt(request).Body.ShouldBe("""{"jsonrpc":"2.0"}""");
    }

    [Fact]
    public void ResponseBuilder_WithBody_SetsJsonContentType()
    {
        var response = ApiGatewayResponseBuilder.Build(new McpDispatchResult(200, "{}"));

        response.StatusCode.ShouldBe(200);
        response.Body.ShouldBe("{}");
        response.Headers["content-type"].ShouldBe("application/json");
    }

    [Fact]
    public void ResponseBuilder_WithEmptyBody_HasNoContentType()
    {
        var response = ApiGatewayResponseBuilder.Build(new McpDispatchResult(202, string.Empty));

        response.StatusCode.ShouldBe(202);
        response.Body.ShouldBeEmpty();
        response.Headers.ShouldBeEmpty();
    }
}
