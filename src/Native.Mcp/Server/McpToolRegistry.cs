using System.Diagnostics.CodeAnalysis;

namespace Native.Mcp;

/// <summary>
/// Read-only lookup of registered tools, consumed by the dispatcher for
/// <c>tools/list</c> and <c>tools/call</c>.
/// </summary>
public interface IMcpToolRegistry
{
    /// <summary>Attempts to resolve a tool descriptor by name.</summary>
    /// <param name="name">The tool name.</param>
    /// <param name="descriptor">The resolved descriptor, if found.</param>
    /// <returns><see langword="true"/> if a tool with that name is registered.</returns>
    bool TryGetTool(string name, [NotNullWhen(true)] out McpToolDescriptor? descriptor);

    /// <summary>Lists all registered tool descriptors, in registration order.</summary>
    /// <returns>The registered descriptors.</returns>
    IReadOnlyList<McpToolDescriptor> ListAll();
}

/// <summary>
/// Default <see cref="IMcpToolRegistry"/>. Built once from the descriptors collected by
/// <see cref="McpServerOptions"/>. Immutable after construction, so it is safe to share as
/// a singleton across concurrent Lambda invocations.
/// </summary>
public sealed class McpToolRegistry : IMcpToolRegistry
{
    private readonly Dictionary<string, McpToolDescriptor> _byName;
    private readonly IReadOnlyList<McpToolDescriptor> _ordered;

    /// <summary>Initializes a new <see cref="McpToolRegistry"/>.</summary>
    /// <param name="descriptors">The tool descriptors to register, in order.</param>
    /// <exception cref="InvalidOperationException">If two tools share a name.</exception>
    public McpToolRegistry(IEnumerable<McpToolDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        var ordered = new List<McpToolDescriptor>();
        _byName = new Dictionary<string, McpToolDescriptor>(StringComparer.Ordinal);

        foreach (var descriptor in descriptors)
        {
            if (!_byName.TryAdd(descriptor.Name, descriptor))
            {
                throw new InvalidOperationException(
                    $"A tool named '{descriptor.Name}' is already registered. Tool names must be unique.");
            }

            ordered.Add(descriptor);
        }

        _ordered = ordered;
    }

    /// <inheritdoc/>
    public bool TryGetTool(string name, [NotNullWhen(true)] out McpToolDescriptor? descriptor)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _byName.TryGetValue(name, out descriptor);
    }

    /// <inheritdoc/>
    public IReadOnlyList<McpToolDescriptor> ListAll() => _ordered;
}
