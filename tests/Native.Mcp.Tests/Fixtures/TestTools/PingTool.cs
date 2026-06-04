using Native.Mcp;

namespace Native.Mcp.Tests.Fixtures;

/// <summary>Empty input for the ping tool.</summary>
public sealed record PingInput;

/// <summary>Output of the ping tool.</summary>
public sealed record PingOutput
{
    /// <summary>Liveness status.</summary>
    public required string Status { get; init; }
}

/// <summary>A trivial health-check tool.</summary>
public sealed partial class PingTool : IMcpTool<PingInput, PingOutput>
{
    /// <inheritdoc/>
    public static string Name => "ping";

    /// <inheritdoc/>
    public static string Description => "Returns ok. Use to verify the server is reachable.";

    /// <inheritdoc/>
    public Task<McpToolResult<PingOutput>> ExecuteAsync(
        PingInput input,
        McpExecutionContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(McpToolResult<PingOutput>.Success(new PingOutput { Status = "ok" }));
}
