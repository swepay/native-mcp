using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using NativeLambdaRouter;
using NSubstitute;
using Native.Mcp.NativeLambdaRouter;
using Native.Mcp.Tests.Fixtures;

namespace Native.Mcp.Tests.NativeLambdaRouter;

public sealed class BridgeTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddNativeMcpServer(options =>
        {
            options.ServerName = "router-mcp";
            options.ServerVersion = "2.0.0";
            options.AddTool<PingTool, PingInput, PingOutput>(
                TestToolsJsonContext.Default.PingInput, TestToolsJsonContext.Default.PingOutput);
        });
        return services.BuildServiceProvider();
    }

    private static APIGatewayHttpApiV2ProxyRequest Request(string rawPath, string method, string? body)
    {
        return new APIGatewayHttpApiV2ProxyRequest
        {
            RawPath = rawPath,
            Body = body,
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["x-correlation-id"] = "router-corr-1",
            },
            RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
            {
                Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription { Method = method, Path = rawPath },
            },
        };
    }

    [Fact]
    public void Mapper_ExtractsBearerCorrelationAndClaims()
    {
        var routeContext = new RouteContext
        {
            Body = "{}",
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["authorization"] = "Bearer router-jwt",
                ["x-correlation-id"] = "c-1",
                ["x-idempotency-key"] = "i-1",
            },
            Claims = new Dictionary<string, string> { ["sub"] = "svc", ["roles"] = "a,b" },
        };

        var ctx = McpRouteContextMapper.ToRequestContext(routeContext);

        ctx.RawJwt.ShouldBe("router-jwt");
        ctx.CorrelationId.ShouldBe("c-1");
        ctx.IdempotencyKey.ShouldBe("i-1");
        ctx.Claims["sub"].ShouldBe("svc");
    }

    [Fact]
    public async Task FunctionHandler_ToolsCall_ReturnsEnvelopeThroughRouter()
    {
        using var provider = BuildProvider();
        var function = new McpRoutedApiGatewayFunction(provider);

        var request = Request("/mcp", "POST",
            """{"jsonrpc":"2.0","id":"1","method":"tools/call","params":{"name":"ping","arguments":{}}}""");

        var response = await function.FunctionHandler(request, Substitute.For<ILambdaContext>());

        response.StatusCode.ShouldBe(200);
        response.Body.ShouldContain("ok");
        response.Body.ShouldContain("router-corr-1");
    }

    [Fact]
    public async Task FunctionHandler_Initialize_RoutesThroughMcp()
    {
        using var provider = BuildProvider();
        var function = new McpRoutedApiGatewayFunction(provider);

        var request = Request("/mcp", "POST", """{"jsonrpc":"2.0","id":"1","method":"initialize"}""");

        var response = await function.FunctionHandler(request, Substitute.For<ILambdaContext>());

        response.StatusCode.ShouldBe(200);
        response.Body.ShouldContain("router-mcp");
        response.Body.ShouldContain("protocolVersion");
    }

    [Fact]
    public async Task FunctionHandler_UnknownRoute_Returns404FromRouter()
    {
        using var provider = BuildProvider();
        var function = new McpRoutedApiGatewayFunction(provider);

        var response = await function.FunctionHandler(
            Request("/not-mcp", "POST", "{}"), Substitute.For<ILambdaContext>());

        response.StatusCode.ShouldBe(404);
    }

    [Fact]
    public async Task FunctionHandler_HealthCheck_HandledByRouter()
    {
        using var provider = BuildProvider();
        var function = new McpRoutedApiGatewayFunction(provider);

        var response = await function.FunctionHandler(
            Request("/health", "GET", null), Substitute.For<ILambdaContext>());

        response.StatusCode.ShouldBe(200);
        response.Body.ShouldContain("healthy");
    }
}
