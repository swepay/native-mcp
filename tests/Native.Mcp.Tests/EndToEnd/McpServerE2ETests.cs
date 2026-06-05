using System.Text.Json;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using Native.FluentValidation.Abstractions;
using Native.Mcp.Testing;
using Native.Mcp.Tests.Fixtures;

namespace Native.Mcp.Tests.EndToEnd;

public sealed class McpServerE2ETests
{
    private static McpTestHost BuildHost(Action<McpTestHostBuilder>? extra = null)
    {
        var builder = McpTestHost.CreateBuilder()
            .WithServerInfo("swepay-test-mcp", "1.2.3")
            .AddTool<PingTool>()
            .AddTool<EchoTool>()
            .AddTool<ScopedTool>()
            .AddTool<FailingTool>()
            .ConfigureServices(s => s.AddSingleton<INativeValidator<EchoInput>, EchoInputValidator>());

        extra?.Invoke(builder);
        return builder.Build();
    }

    [Fact]
    public async Task Initialize_ReturnsServerInfoAndProtocol()
    {
        await using var host = BuildHost();
        var client = host.CreateClient();

        var response = await client.InitializeAsync();

        response.HttpStatusCode.ShouldBe(200);
        response.ProtocolError.ShouldBeNull();
        response.ProtocolVersion.ShouldBe(Native.Mcp.Protocol.McpProtocol.Version);
        response.ServerName.ShouldBe("swepay-test-mcp");
        response.ServerVersion.ShouldBe("1.2.3");
    }

    [Fact]
    public async Task ToolsList_ListsRegisteredToolsWithSchemas()
    {
        await using var host = BuildHost();
        var client = host.CreateClient();

        var response = await client.ListToolsAsync();

        var toolNames = response.Tools.Select(t => t.Name).ToList();
        toolNames.ShouldContain("ping");
        toolNames.ShouldContain("echo");
        toolNames.ShouldContain("scoped");
        toolNames.ShouldContain("failing");

        var echo = response.Tools.Single(t => t.Name == "echo");
        var echoSchema = echo.InputSchema!.ToJsonString();
        echoSchema.ShouldContain("\"message\"");
        echoSchema.ShouldContain("\"maxLength\"");
    }

    [Fact]
    public async Task ToolsCall_Ping_ReturnsCanonicalSuccessEnvelope()
    {
        await using var host = BuildHost();
        var client = host.CreateClient();

        var response = await client.CallToolAsync("ping", new { }, new McpRequestOptions
        {
            CorrelationId = "corr-1",
            Scopes = ["test:read"],
        });

        response.ShouldBeSuccessful();
        response.Envelope!.DataAs<PingOutput>()!.Status.ShouldBe("ok");
        response.Envelope.Metadata!.CorrelationId.ShouldBe("corr-1");
        response.Envelope.Metadata.ServerName.ShouldBe("swepay-test-mcp");
    }

    [Fact]
    public async Task ToolsCall_Echo_RoundTripsMessage()
    {
        await using var host = BuildHost();
        var client = host.CreateClient();

        var response = await client.CallToolAsync("echo", new { message = "hello world" });

        response.ShouldBeSuccessful();
        response.Envelope!.DataAs<EchoOutput>()!.Echo.ShouldBe("hello world");
    }

    [Fact]
    public async Task ToolsCall_Echo_WithEmptyMessage_FailsValidation()
    {
        await using var host = BuildHost();
        var client = host.CreateClient();

        var response = await client.CallToolAsync("echo", new { message = "" });

        response.ShouldBeError().ShouldHaveProblemType(ProblemTypes.ValidationFailed);
        response.IsError.ShouldBeTrue();
    }

    [Fact]
    public async Task ToolsCall_Scoped_WithoutScope_ReturnsForbidden()
    {
        await using var host = BuildHost();
        var client = host.CreateClient();

        var response = await client.CallToolAsync("scoped", new { value = "x" });

        response.ShouldBeError().ShouldHaveProblemType(ProblemTypes.Forbidden);
    }

    [Fact]
    public async Task ToolsCall_Scoped_WithScope_Succeeds()
    {
        await using var host = BuildHost();
        var client = host.CreateClient();

        var response = await client.CallToolAsync("scoped", new { value = "x" }, new McpRequestOptions
        {
            Scopes = [ScopedTool.RequiredScope],
        });

        response.ShouldBeSuccessful();
        response.Envelope!.DataAs<ScopedOutput>()!.Value.ShouldBe("x");
    }

    [Fact]
    public async Task ToolsCall_FailingTool_ReturnsInternalError_WithoutLeakingDetails()
    {
        await using var host = BuildHost();
        var client = host.CreateClient();

        var response = await client.CallToolAsync("failing", new { note = "n" });

        response.ShouldBeError().ShouldHaveProblemType(ProblemTypes.InternalError);
        response.RawJson.ShouldNotContain(FailingTool.SecretMessage);
        response.RawJson.ShouldNotContain("InvalidOperationException");
        response.Envelope!.Error!.Detail.ShouldNotContain(FailingTool.SecretMessage);
    }

    [Fact]
    public async Task ToolsCall_UnknownTool_ReturnsInvalidParams()
    {
        await using var host = BuildHost();
        var client = host.CreateClient();

        var response = await client.CallToolAsync("does_not_exist", new { });

        response.ShouldBeProtocolError(Native.Mcp.Protocol.JsonRpcErrorCodes.InvalidParams);
    }

    [Fact]
    public async Task UnknownMethod_ReturnsMethodNotFound()
    {
        await using var host = BuildHost();
        var client = host.CreateClient();

        var raw = await client.SendRawAsync(
            """{"jsonrpc":"2.0","id":"1","method":"resources/list"}""");

        ErrorCode(raw.Body).ShouldBe(Native.Mcp.Protocol.JsonRpcErrorCodes.MethodNotFound);
        raw.HttpStatusCode.ShouldBe(200);
    }

    [Fact]
    public async Task MalformedBody_ReturnsParseError()
    {
        await using var host = BuildHost();
        var client = host.CreateClient();

        var raw = await client.SendRawAsync("{ this is not json ");

        ErrorCode(raw.Body).ShouldBe(Native.Mcp.Protocol.JsonRpcErrorCodes.ParseError);
        raw.HttpStatusCode.ShouldBe(200);
    }

    [Fact]
    public async Task Notification_ReturnsAcceptedWithEmptyBody()
    {
        await using var host = BuildHost();
        var client = host.CreateClient();

        var raw = await client.SendRawAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");

        raw.HttpStatusCode.ShouldBe(202);
        raw.Body.ShouldBeEmpty();
    }

    [Fact]
    public async Task AddDiscoveredTools_RegistersAllTools()
    {
        await using var host = McpTestHost.CreateBuilder()
            .WithServerInfo("disco", "1.0.0")
            .Build();

        // Re-build a host using the generated discovery helper + the generated JSON context.
        var services = new ServiceCollection();
        services.AddNativeMcpServer(options => options.AddDiscoveredTools(TestToolsJsonContext.Default));
        await using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IMcpToolRegistry>();

        var names = registry.ListAll().Select(d => d.Name).ToList();
        names.ShouldContain("ping");
        names.ShouldContain("echo");
        names.ShouldContain("scoped");
        names.ShouldContain("failing");
    }

    private static int ErrorCode(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("error").GetProperty("code").GetInt32();
    }
}
