using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Native.Mcp;

namespace Native.Mcp.Sample;

/// <summary>Empty input for the ping tool.</summary>
public sealed record PingInput;

/// <summary>Output of the ping tool.</summary>
public sealed record PingOutput
{
    /// <summary>Liveness status.</summary>
    public required string Status { get; init; }

    /// <summary>The authenticated subject, if any (demonstrates claim access).</summary>
    public string? Subject { get; init; }
}

/// <summary>A trivial health-check tool.</summary>
public sealed partial class PingTool : IMcpTool<PingInput, PingOutput>
{
    /// <inheritdoc/>
    public static string Name => "ping";

    /// <inheritdoc/>
    public static string Description => "Returns ok. Use to verify the server is reachable and authenticated.";

    /// <inheritdoc/>
    public Task<McpToolResult<PingOutput>> ExecuteAsync(
        PingInput input,
        McpExecutionContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(McpToolResult<PingOutput>.Success(new PingOutput
        {
            Status = "ok",
            Subject = string.IsNullOrEmpty(context.Subject) ? null : context.Subject,
        }));
}

/// <summary>Input for the echo tool.</summary>
public sealed record EchoInput
{
    /// <summary>The message to echo back.</summary>
    [Required]
    [Description("The message to echo back (1-280 printable characters).")]
    [RegularExpression(@"^.{1,280}$")]
    public required string Message { get; init; }
}

/// <summary>Output of the echo tool.</summary>
public sealed record EchoOutput
{
    /// <summary>The echoed message.</summary>
    public required string Echo { get; init; }
}

/// <summary>Echoes the input message. Requires the <c>sample:echo</c> scope (defense in depth).</summary>
public sealed partial class EchoTool : IMcpTool<EchoInput, EchoOutput>
{
    /// <summary>Scope required to call this tool.</summary>
    public const string RequiredScope = "sample:echo";

    /// <inheritdoc/>
    public static string Name => "echo";

    /// <inheritdoc/>
    public static string Description => "Echoes the provided message. Requires the sample:echo scope.";

    /// <inheritdoc/>
    public Task<McpToolResult<EchoOutput>> ExecuteAsync(
        EchoInput input,
        McpExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (!context.HasScope(RequiredScope))
        {
            return Task.FromResult(McpToolResult<EchoOutput>.Failure(
                McpProblems.Forbidden($"Required scope: {RequiredScope}", context.RequestId)));
        }

        return Task.FromResult(McpToolResult<EchoOutput>.Success(new EchoOutput { Echo = input.Message }));
    }
}
