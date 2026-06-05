using Native.Mcp;

namespace Native.Mcp.Tests.Fixtures;

/// <summary>A tool that authorizes via <see cref="McpExecutionContext.RequireRole"/> (the throwing guard).</summary>
public sealed partial class RequireRoleTool : IMcpTool<ScopedInput, ScopedOutput>
{
    /// <summary>The role required to call this tool.</summary>
    public const string RequiredRole = "test-writer";

    /// <inheritdoc/>
    public static string Name => "require_role";

    /// <inheritdoc/>
    public static string Description => "Requires the test-writer role via RequireRole. Returns the value.";

    /// <inheritdoc/>
    public Task<McpToolResult<ScopedOutput>> ExecuteAsync(
        ScopedInput input,
        McpExecutionContext context,
        CancellationToken cancellationToken)
    {
        context.RequireRole(RequiredRole);
        return Task.FromResult(McpToolResult<ScopedOutput>.Success(new ScopedOutput { Value = input.Value }));
    }
}
