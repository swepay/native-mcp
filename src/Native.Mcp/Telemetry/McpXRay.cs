namespace Native.Mcp.Telemetry;

/// <summary>
/// Abstraction over distributed tracing subsegments. The runtime opens a subsegment per
/// phase (<c>mcp.parse</c>, <c>mcp.dispatch</c>, <c>mcp.tool.execute</c>,
/// <c>mcp.serialize</c>). The default implementation is a no-op; consumers that want AWS
/// X-Ray plug in an implementation that wraps the X-Ray recorder. Kept as a seam because the
/// X-Ray SDK is not Native AOT friendly and is opt-in per service.
/// </summary>
public interface IMcpTracer
{
    /// <summary>Begins a subsegment. Dispose the returned handle to close it.</summary>
    /// <param name="name">The subsegment name.</param>
    /// <returns>A disposable that closes the subsegment.</returns>
    IDisposable BeginSubsegment(string name);
}

/// <summary>Well-known subsegment names emitted by the runtime.</summary>
public static class McpTraceSegments
{
    /// <summary>Parsing the JSON-RPC request body.</summary>
    public const string Parse = "mcp.parse";

    /// <summary>Routing the request to a method handler.</summary>
    public const string Dispatch = "mcp.dispatch";

    /// <summary>Executing the tool.</summary>
    public const string ToolExecute = "mcp.tool.execute";

    /// <summary>Serializing the envelope and response.</summary>
    public const string Serialize = "mcp.serialize";
}

/// <summary>A no-op <see cref="IMcpTracer"/> used when tracing is not configured.</summary>
public sealed class NoOpMcpTracer : IMcpTracer
{
    private static readonly NoOpSubsegment Subsegment = new();

    /// <summary>Singleton instance.</summary>
    public static readonly NoOpMcpTracer Instance = new();

    private NoOpMcpTracer()
    {
    }

    /// <inheritdoc/>
    public IDisposable BeginSubsegment(string name) => Subsegment;

    private sealed class NoOpSubsegment : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
