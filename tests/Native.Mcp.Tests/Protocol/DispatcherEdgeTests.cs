using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Native.FluentValidation.Abstractions;
using Native.Mcp.Protocol;
using Native.Mcp.Testing;
using Native.Mcp.Tests.Fixtures;

namespace Native.Mcp.Tests.Protocol;

public sealed class DispatcherEdgeTests
{
    private static McpTestHost BuildHost() =>
        McpTestHost.CreateBuilder()
            .AddTool<PingTool>()
            .AddTool<EchoTool>()
            .ConfigureServices(s => s.AddSingleton<INativeValidator<EchoInput>, EchoInputValidator>())
            .Build();

    private static (int? Code, JsonElement Root) Parse(string body)
    {
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement.Clone();
        int? code = root.TryGetProperty("error", out var e) ? e.GetProperty("code").GetInt32() : null;
        return (code, root);
    }

    [Fact]
    public async Task WrongJsonRpcVersion_ReturnsInvalidRequest()
    {
        await using var host = BuildHost();
        var raw = await host.CreateClient().SendRawAsync("""{"jsonrpc":"1.0","id":"1","method":"ping"}""");

        Parse(raw.Body).Code.Should().Be(JsonRpcErrorCodes.InvalidRequest);
    }

    [Fact]
    public async Task MissingMethod_ReturnsInvalidRequest()
    {
        await using var host = BuildHost();
        var raw = await host.CreateClient().SendRawAsync("""{"jsonrpc":"2.0","id":"1"}""");

        Parse(raw.Body).Code.Should().Be(JsonRpcErrorCodes.InvalidRequest);
    }

    [Fact]
    public async Task ToolsCall_WithoutParams_ReturnsInvalidParams()
    {
        await using var host = BuildHost();
        var raw = await host.CreateClient().SendRawAsync("""{"jsonrpc":"2.0","id":"1","method":"tools/call"}""");

        Parse(raw.Body).Code.Should().Be(JsonRpcErrorCodes.InvalidParams);
    }

    [Fact]
    public async Task ToolsCall_NumericId_IsEchoed()
    {
        await using var host = BuildHost();
        var raw = await host.CreateClient().SendRawAsync(
            """{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"ping","arguments":{}}}""");

        var (_, root) = Parse(raw.Body);
        root.GetProperty("id").GetInt32().Should().Be(5);
        root.TryGetProperty("result", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ToolsCall_WithoutArguments_UsesEmptyObject()
    {
        await using var host = BuildHost();
        var response = await host.CreateClient().SendRawAsync(
            """{"jsonrpc":"2.0","id":"1","method":"tools/call","params":{"name":"ping"}}""");

        response.HttpStatusCode.Should().Be(200);
        response.Body.Should().Contain("ok");
    }

    [Fact]
    public async Task ToolsCall_MalformedArguments_ReturnsValidationFailedEnvelope()
    {
        await using var host = BuildHost();
        // 'message' must be a string; passing a number forces a deserialization failure.
        var response = await host.CreateClient().SendRawAsync(
            """{"jsonrpc":"2.0","id":"1","method":"tools/call","params":{"name":"echo","arguments":{"message":123}}}""");

        response.Body.Should().Contain(ProblemTypes.ValidationFailed);
        var (_, root) = Parse(response.Body);
        root.GetProperty("result").GetProperty("isError").GetBoolean().Should().BeTrue();
    }
}
