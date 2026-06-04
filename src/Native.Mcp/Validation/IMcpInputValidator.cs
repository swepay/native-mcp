namespace Native.Mcp.Validation;

/// <summary>
/// Validates a tool's deserialized input before execution. The default implementation
/// bridges to <c>Native.FluentValidation</c> validators resolved from DI. A pass-through
/// implementation is used when validation is not configured.
/// </summary>
public interface IMcpInputValidator
{
    /// <summary>
    /// Validates <paramref name="input"/> for the given tool.
    /// </summary>
    /// <param name="descriptor">The tool descriptor.</param>
    /// <param name="input">The deserialized input instance (boxed).</param>
    /// <param name="serviceProvider">The request-scoped service provider.</param>
    /// <param name="context">The execution context (for request id / correlation).</param>
    /// <returns>
    /// <see langword="null"/> if the input is valid; otherwise a populated
    /// <see cref="SwepayProblemDetails"/> describing the validation failure.
    /// </returns>
    SwepayProblemDetails? Validate(
        McpToolDescriptor descriptor,
        object input,
        IServiceProvider serviceProvider,
        McpExecutionContext context);
}

/// <summary>An <see cref="IMcpInputValidator"/> that performs no validation.</summary>
public sealed class PassThroughInputValidator : IMcpInputValidator
{
    /// <summary>Singleton instance.</summary>
    public static readonly PassThroughInputValidator Instance = new();

    private PassThroughInputValidator()
    {
    }

    /// <inheritdoc/>
    public SwepayProblemDetails? Validate(
        McpToolDescriptor descriptor,
        object input,
        IServiceProvider serviceProvider,
        McpExecutionContext context) => null;
}
