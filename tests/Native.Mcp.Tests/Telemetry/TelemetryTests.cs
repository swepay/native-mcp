using System.Text.Json;
using FluentAssertions;
using Native.Mcp.Telemetry;
using Native.Mcp.Testing;

namespace Native.Mcp.Tests.Telemetry;

public sealed class TelemetryTests
{
    [Fact]
    public void McpMetrics_ToolCall_EmitsValidEmf()
    {
        var writer = new StringWriter();
        var metrics = new McpMetrics(writer, TimeProvider.System, "Swepay/Mcp");

        metrics.RecordToolCall("ping", success: true, durationMs: 7);

        var line = writer.ToString().Trim();
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;

        root.GetProperty("tool_name").GetString().Should().Be("ping");
        root.GetProperty("success").GetString().Should().Be("true");
        root.GetProperty("mcp.tools.call.count").GetInt32().Should().Be(1);
        root.GetProperty("mcp.tools.call.duration_ms").GetInt64().Should().Be(7);

        var aws = root.GetProperty("_aws");
        aws.GetProperty("Timestamp").GetInt64().Should().BeGreaterThan(0);
        var cw = aws.GetProperty("CloudWatchMetrics")[0];
        cw.GetProperty("Namespace").GetString().Should().Be("Swepay/Mcp");
        cw.GetProperty("Metrics").EnumerateArray()
            .Select(m => m.GetProperty("Name").GetString())
            .Should().Contain("mcp.tools.call.count");
    }

    [Fact]
    public void McpMetrics_ProtocolError_EmitsDimension()
    {
        var writer = new StringWriter();
        var metrics = new McpMetrics(writer, TimeProvider.System);

        metrics.RecordProtocolError(-32601);

        using var doc = JsonDocument.Parse(writer.ToString().Trim());
        doc.RootElement.GetProperty("error_code").GetString().Should().Be("-32601");
        doc.RootElement.GetProperty("mcp.protocol.errors.count").GetInt32().Should().Be(1);
    }

    [Fact]
    public void McpLogger_WritesStructuredFields_AndOmitsPii()
    {
        var writer = new StringWriter();
        var logger = new McpLogger(writer);
        var context = new McpExecutionContextBuilder()
            .WithToolName("provision")
            .WithCorrelationId("trial-01")
            .WithRawJwt("super-secret-jwt-value")
            .WithClaim("email", "alice@acme.com.br")
            .WithClaim("client_id", "trial-provisioner-bot")
            .Build();

        logger.LogToolExecuted(context, "success", durationMs: 12, envelopeSuccess: true);

        var raw = writer.ToString();
        raw.Should().NotContain("super-secret-jwt-value");
        raw.Should().NotContain("alice@acme.com.br");

        using var doc = JsonDocument.Parse(raw.Trim());
        var root = doc.RootElement;
        root.GetProperty("toolName").GetString().Should().Be("provision");
        root.GetProperty("correlationId").GetString().Should().Be("trial-01");
        root.GetProperty("serviceAccountId").GetString().Should().Be("trial-provisioner-bot");
        root.GetProperty("outcome").GetString().Should().Be("success");
        root.GetProperty("durationMs").GetInt32().Should().Be(12);
        root.GetProperty("envelopeSuccess").GetBoolean().Should().BeTrue();
        root.GetProperty("level").GetString().Should().Be("INFO");
    }
}
