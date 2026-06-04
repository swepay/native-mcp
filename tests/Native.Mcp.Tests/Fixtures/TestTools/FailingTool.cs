using Native.Mcp;

namespace Native.Mcp.Tests.Fixtures;

/// <summary>Input for the failing tool.</summary>
public sealed record FailingInput
{
    /// <summary>Ignored; the tool always throws.</summary>
    public string? Note { get; init; }
}

/// <summary>Never produced (the tool always throws).</summary>
public sealed record FailingOutput
{
    /// <summary>Unused.</summary>
    public string? Value { get; init; }
}

/// <summary>A tool that always throws, to exercise the no-leak internal-error path.</summary>
public sealed partial class FailingTool : IMcpTool<FailingInput, FailingOutput>
{
    /// <summary>A secret string that must never appear in any response.</summary>
    public const string SecretMessage = "SECRET-connection-string-and-stack-trace";

    /// <inheritdoc/>
    public static string Name => "failing";

    /// <inheritdoc/>
    public static string Description => "Always throws. Used only in tests.";

    /// <inheritdoc/>
    public Task<McpToolResult<FailingOutput>> ExecuteAsync(
        FailingInput input,
        McpExecutionContext context,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException(SecretMessage);
}
