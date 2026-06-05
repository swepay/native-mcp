using Shouldly;
using Native.Mcp.Protocol;
using Native.Mcp.Telemetry;
using Native.Mcp.Validation;
using Native.Mcp.Tests.Fixtures;

namespace Native.Mcp.Tests.Contracts;

public sealed class ContractsExtraTests
{
    private static McpExecutionContext Context(DateTimeOffset startedAt) => new(
        toolName: "t",
        requestId: "r",
        correlationId: "c",
        claims: new Dictionary<string, string>(),
        rawJwt: "jwt-value",
        startedAt: startedAt,
        idempotencyKey: "idem-1");

    [Fact]
    public void SwepayEnvelope_Poco_RoundTrips()
    {
        var metadata = new SwepayEnvelopeMetadata("r", "c", "2026-06-03T00:00:00.000Z", 5, "srv", "1.0.0");
        var envelope = new SwepayEnvelope<PingOutput>
        {
            Success = true,
            Data = new PingOutput { Status = "ok" },
            Error = null,
            Metadata = metadata,
        };

        envelope.Success.ShouldBeTrue();
        envelope.Data!.Status.ShouldBe("ok");
        envelope.Metadata.ServerName.ShouldBe("srv");
        envelope.Error.ShouldBeNull();
    }

    [Fact]
    public void McpToolResult_GuardsAgainstNull()
    {
        var success = () => McpToolResult<PingOutput>.Success(null!);
        var failure = () => McpToolResult<PingOutput>.Failure(null!);

        Should.Throw<ArgumentNullException>(success);
        Should.Throw<ArgumentNullException>(failure);
    }

    [Fact]
    public void McpContent_FromText_BuildsTextBlock()
    {
        var content = McpContent.FromText("hello");

        content.Type.ShouldBe("text");
        content.Text.ShouldBe("hello");
    }

    [Fact]
    public void ExecutionContext_ExposesIdempotencyAndTiming()
    {
        var started = DateTimeOffset.UtcNow;
        var ctx = Context(started);

        ctx.IdempotencyKey.ShouldBe("idem-1");
        ctx.StartedAt.ShouldBe(started);
        ctx.RawJwt.ShouldBe("jwt-value");
    }

    [Fact]
    public void SwepayProblemDetails_Create_GuardsAgainstEmptyType()
    {
        var act = () => SwepayProblemDetails.Create("", "Title", 400, "detail");
        Should.Throw<ArgumentException>(act);
    }

    [Fact]
    public void NoOpTelemetry_AndPassThroughValidator_DoNothing()
    {
        NoOpMcpMetrics.Instance.RecordToolCall("t", true, 1);
        NoOpMcpMetrics.Instance.RecordToolsList();
        NoOpMcpMetrics.Instance.RecordInitialize();
        NoOpMcpMetrics.Instance.RecordProtocolError(-1);
        NoOpMcpMetrics.Instance.RecordValidationFailure("t");
        NoOpMcpMetrics.Instance.RecordScopeDenied("t", "s");
        NoOpMcpToolLogger.Instance.LogToolExecuted(Context(DateTimeOffset.UtcNow), "x", 1, true);
        using (NoOpMcpTracer.Instance.BeginSubsegment(McpTraceSegments.Parse))
        {
        }

        var problem = PassThroughInputValidator.Instance.Validate(
            Descriptor(), new object(), EmptyProvider.Instance, Context(DateTimeOffset.UtcNow));

        problem.ShouldBeNull();
    }

    private static McpToolDescriptor Descriptor() => new(
        "n", "d", "{}", typeof(object), typeof(object), typeof(object),
        _ => new object(),
        (_, _) => System.Array.Empty<McpValidationFailure>(),
        (_, _, _, _) => Task.FromResult(McpToolInvocationResult.Success(() => null)));

    private sealed class EmptyProvider : IServiceProvider
    {
        public static readonly EmptyProvider Instance = new();

        public object? GetService(Type serviceType) => null;
    }
}
