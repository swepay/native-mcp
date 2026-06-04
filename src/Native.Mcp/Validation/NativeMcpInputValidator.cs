namespace Native.Mcp.Validation;

/// <summary>
/// Default <see cref="IMcpInputValidator"/>. Delegates to the descriptor's captured
/// (AOT-safe) validation delegate, which resolves an
/// <c>INativeValidator&lt;TInput&gt;</c> from DI, then maps any failures into a canonical
/// <see cref="SwepayProblemDetails"/>. Messages are end-user safe (no internal detail).
/// </summary>
public sealed class NativeMcpInputValidator : IMcpInputValidator
{
    /// <summary>Singleton instance.</summary>
    public static readonly NativeMcpInputValidator Instance = new();

    private NativeMcpInputValidator()
    {
    }

    /// <inheritdoc/>
    public SwepayProblemDetails? Validate(
        McpToolDescriptor descriptor,
        object input,
        IServiceProvider serviceProvider,
        McpExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(context);

        var failures = descriptor.ValidateInput(serviceProvider, input);
        if (failures.Count == 0)
        {
            return null;
        }

        var detail = string.Join("; ", failures.Select(static f =>
            string.IsNullOrEmpty(f.PropertyName) ? f.Message : $"{f.PropertyName}: {f.Message}"));

        return McpProblems.ValidationFailed(detail, context.RequestId);
    }
}
