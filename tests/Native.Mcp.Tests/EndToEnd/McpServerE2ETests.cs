using System.Text.Json;
using FluentAssertions;
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

        response.HttpStatusCode.Should().Be(200);
        response.ProtocolError.Should().BeNull();
        response.ProtocolVersion.Should().Be(Native.Mcp.Protocol.McpProtocol.Version);
        response.ServerName.Should().Be("swepay-test-mcp");
        response.ServerVersion.Should().Be("1.2.3");
    }

    [Fact]
    public async Task ToolsList_ListsRegisteredToolsWithSchemas()
    {
        await using var host = BuildHost();
        var client = host.CreateClient();

        var response = await client.ListToolsAsync();

        response.Tools.Select(t => t.Name).Should().Contain(["ping", "echo", "scoped", "failing"]);
        var echo = response.Tools.Single(t => t.Name == "echo");
        echo.InputSchema!.ToJsonString().Should().Contain("\"message\"").And.Contain("\"maxLength\"");
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

        response.Should().BeSuccessful();
        response.Envelope!.DataAs<PingOutput>()!.Status.Should().Be("ok");
        response.Envelope.Metadata!.CorrelationId.Should().Be("corr-1");
        response.Envelope.Metadata.ServerName.Should().Be("swepay-test-mcp");
    }

    [Fact]
    public async Task ToolsCall_Echo_RoundTripsMessage()
    {
        await using var host = BuildHost();
        var client = host.CreateClient();

        var response = await client.CallToolAsync("echo", new { message = "hello world" });

        response.Should().BeSuccessful();
        response.Envelope!.DataAs<EchoOutput>()!.Echo.Should().Be("hello world");
    }

    [Fact]
    public async Task ToolsCall_Echo_WithEmptyMessage_FailsValidation()
    {
        await using var host = BuildHost();
        var client = host.CreateClient();

        var response = await client.CallToolAsync("echo", new { message = "" });

        response.Should().BeError().WithProblemType(ProblemTypes.ValidationFailed);
        response.IsError.Should().BeTrue();
    }

    [Fact]
    public async Task ToolsCall_Scoped_WithoutScope_ReturnsForbidden()
    {
        await using var host = BuildHost();
        var client = host.CreateClient();

        var response = await client.CallToolAsync("scoped", new { value = "x" });

        response.Should().BeError().WithProblemType(ProblemTypes.Forbidden);
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

        response.Should().BeSuccessful();
        response.Envelope!.DataAs<ScopedOutput>()!.Value.Should().Be("x");
    }

    [Fact]
    public async Task ToolsCall_FailingTool_ReturnsInternalError_WithoutLeakingDetails()
    {
        await using var host = BuildHost();
        var client = host.CreateClient();

        var response = await client.CallToolAsync("failing", new { note = "n" });

        response.Should().BeError().WithProblemType(ProblemTypes.InternalError);
        response.RawJson.Should().NotContain(FailingTool.SecretMessage);
        response.RawJson.Should().NotContain("InvalidOperationException");
        response.Envelope!.Error!.Detail.Should().NotContain(FailingTool.SecretMessage);
    }

    [Fact]
    public async Task ToolsCall_UnknownTool_ReturnsInvalidParams()
    {
        await using var host = BuildHost();
        var client = host.CreateClient();

        var response = await client.CallToolAsync("does_not_exist", new { });

        response.Should().BeProtocolError(Native.Mcp.Protocol.JsonRpcErrorCodes.InvalidParams);
    }

    [Fact]
    public async Task UnknownMethod_ReturnsMethodNotFound()
    {
        await using var host = BuildHost();
        var client = host.CreateClient();

        var raw = await client.SendRawAsync(
            """{"jsonrpc":"2.0","id":"1","method":"resources/list"}""");

        ErrorCode(raw.Body).Should().Be(Native.Mcp.Protocol.JsonRpcErrorCodes.MethodNotFound);
        raw.HttpStatusCode.Should().Be(200);
    }

    [Fact]
    public async Task MalformedBody_ReturnsParseError()
    {
        await using var host = BuildHost();
        var client = host.CreateClient();

        var raw = await client.SendRawAsync("{ this is not json ");

        ErrorCode(raw.Body).Should().Be(Native.Mcp.Protocol.JsonRpcErrorCodes.ParseError);
        raw.HttpStatusCode.Should().Be(200);
    }

    [Fact]
    public async Task Notification_ReturnsAcceptedWithEmptyBody()
    {
        await using var host = BuildHost();
        var client = host.CreateClient();

        var raw = await client.SendRawAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");

        raw.HttpStatusCode.Should().Be(202);
        raw.Body.Should().BeEmpty();
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

        registry.ListAll().Select(d => d.Name).Should()
            .Contain(["ping", "echo", "scoped", "failing"]);
    }

    private static int ErrorCode(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("error").GetProperty("code").GetInt32();
    }
}
