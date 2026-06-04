namespace Native.Mcp;

/// <summary>
/// Contract for an MCP tool. Implementations are registered via
/// <c>McpServerOptions.AddTool&lt;TTool, TInput, TOutput&gt;(...)</c> (or the
/// generated <c>AddDiscoveredTools</c> helper).
/// </summary>
/// <typeparam name="TInput">The tool input (arguments) type. Must be a reference type.</typeparam>
/// <typeparam name="TOutput">The tool output (result data) type. Must be a reference type.</typeparam>
public interface IMcpTool<TInput, TOutput>
    where TInput : class
    where TOutput : class
{
    /// <summary>
    /// Unique tool name. <c>snake_case</c>. Used by clients in
    /// <c>tools/call</c> <c>params.name</c>.
    /// </summary>
    static abstract string Name { get; }

    /// <summary>
    /// Human-readable description shown in <c>tools/list</c>. Should explain what the
    /// tool does AND when not to use it (its boundaries).
    /// </summary>
    static abstract string Description { get; }

    /// <summary>
    /// JSON Schema (Draft 2020-12) describing <typeparamref name="TInput"/>.
    /// The <c>Native.Mcp.SourceGenerator</c> provides this automatically as a
    /// generated static member on the implementing type. The default returns a
    /// permissive object schema so tools compile without the generator.
    /// </summary>
    static virtual string InputSchemaJson => """{"type":"object"}""";

    /// <summary>
    /// Executes the tool. <paramref name="input"/> is already validated when this is
    /// called. Implementations must not throw for expected failures — return
    /// <see cref="McpToolResult{T}.Failure(SwepayProblemDetails)"/> instead. Exceptions
    /// are reserved for bugs and are mapped to a generic internal-error envelope.
    /// </summary>
    /// <param name="input">The validated, deserialized input.</param>
    /// <param name="context">Execution context (claims, correlation id, timing).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A success or failure result.</returns>
    Task<McpToolResult<TOutput>> ExecuteAsync(
        TInput input,
        McpExecutionContext context,
        CancellationToken cancellationToken);
}
