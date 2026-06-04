using Native.Mcp;

namespace Native.Mcp.Tests.Fixtures;

/// <summary>Input for the scoped tool.</summary>
public sealed record ScopedInput
{
    /// <summary>An arbitrary value to return on success.</summary>
    public string? Value { get; init; }
}

/// <summary>Output of the scoped tool.</summary>
public sealed record ScopedOutput
{
    /// <summary>The echoed value.</summary>
    public string? Value { get; init; }
}

/// <summary>A tool that enforces a scope as defense in depth (returns forbidden if missing).</summary>
public sealed partial class ScopedTool : IMcpTool<ScopedInput, ScopedOutput>
{
    /// <summary>The scope required to call this tool.</summary>
    public const string RequiredScope = "test:write";

    /// <inheritdoc/>
    public static string Name => "scoped";

    /// <inheritdoc/>
    public static string Description => "Requires the test:write scope. Returns the provided value.";

    /// <inheritdoc/>
    public Task<McpToolResult<ScopedOutput>> ExecuteAsync(
        ScopedInput input,
        McpExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (!context.HasScope(RequiredScope))
        {
            return Task.FromResult(McpToolResult<ScopedOutput>.Failure(
                McpProblems.Forbidden($"Required scope: {RequiredScope}", context.RequestId)));
        }

        return Task.FromResult(McpToolResult<ScopedOutput>.Success(new ScopedOutput { Value = input.Value }));
    }
}
