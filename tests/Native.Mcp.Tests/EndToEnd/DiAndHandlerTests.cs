using Amazon.Lambda.APIGatewayEvents;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using Native.Mcp.ApiGateway;
using Native.Mcp.Telemetry;
using Native.Mcp.Tests.Fixtures;

namespace Native.Mcp.Tests.EndToEnd;

public sealed class DiAndHandlerTests
{
    private static ServiceProvider BuildProvider(bool telemetry = true)
    {
        var services = new ServiceCollection();
        services.AddNativeMcpServer(options =>
        {
            options.ServerName = "handler-host";
            options.ServerVersion = "2.0.0";
            options.AddTool<PingTool, PingInput, PingOutput>(
                TestToolsJsonContext.Default.PingInput, TestToolsJsonContext.Default.PingOutput);
        });

        if (telemetry)
        {
            services.AddNativeMcpTelemetry();
        }

        services.AddNativeMcpApiGatewayHandler();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task LambdaHandler_RoutesToolCall_ReturnsHttp200Envelope()
    {
        using var provider = BuildProvider();
        var handler = provider.GetRequiredService<McpLambdaHandler>();

        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            Headers = new Dictionary<string, string> { ["x-correlation-id"] = "h-1" },
            Body = """{"jsonrpc":"2.0","id":"1","method":"tools/call","params":{"name":"ping","arguments":{}}}""",
        };

        var response = await handler.HandleAsync(request);

        response.StatusCode.ShouldBe(200);
        response.Headers["content-type"].ShouldBe("application/json");
        response.Body.ShouldContain("ok");
        response.Body.ShouldContain("h-1");
    }

    [Fact]
    public async Task LambdaHandler_Notification_Returns202()
    {
        using var provider = BuildProvider(telemetry: false);
        var handler = provider.GetRequiredService<McpLambdaHandler>();

        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            Body = """{"jsonrpc":"2.0","method":"notifications/initialized"}""",
        };

        var response = await handler.HandleAsync(request);

        response.StatusCode.ShouldBe(202);
        response.Body.ShouldBeEmpty();
    }

    [Fact]
    public void AddNativeMcpTelemetry_RegistersEmfAndStructuredLogger()
    {
        using var provider = BuildProvider();

        provider.GetRequiredService<IMcpMetrics>().ShouldBeOfType<McpMetrics>();
        provider.GetRequiredService<IMcpToolLogger>().ShouldBeOfType<McpLogger>();
    }

    [Fact]
    public void WithoutTelemetry_DefaultsAreNoOp()
    {
        using var provider = BuildProvider(telemetry: false);

        provider.GetRequiredService<IMcpMetrics>().ShouldBeSameAs(NoOpMcpMetrics.Instance);
        provider.GetRequiredService<IMcpToolLogger>().ShouldBeSameAs(NoOpMcpToolLogger.Instance);
        provider.GetRequiredService<IMcpTracer>().ShouldBeSameAs(NoOpMcpTracer.Instance);
    }
}
