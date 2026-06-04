using Microsoft.Extensions.DependencyInjection;

namespace Native.Mcp.Testing;

/// <summary>
/// An in-memory MCP server host for tests. Owns the DI container and creates
/// <see cref="McpTestClient"/> instances that drive the dispatcher directly (no HTTP).
/// </summary>
public sealed class McpTestHost : IAsyncDisposable
{
    private readonly ServiceProvider _serviceProvider;

    internal McpTestHost(ServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>Starts building a host.</summary>
    /// <returns>A new <see cref="McpTestHostBuilder"/>.</returns>
    public static McpTestHostBuilder CreateBuilder() => new();

    /// <summary>The host's root service provider (for resolving tool dependencies in assertions).</summary>
    public IServiceProvider Services => _serviceProvider;

    /// <summary>Creates a client bound to this host.</summary>
    /// <returns>A new <see cref="McpTestClient"/>.</returns>
    public McpTestClient CreateClient() => new(_serviceProvider);

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();
}
