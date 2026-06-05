using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Native.Mcp.ApiGateway;

/// <summary>
/// DI helpers for hosting an MCP server directly behind API Gateway HTTP API v2 (without
/// NativeLambdaRouter).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="McpLambdaHandler"/> entry point. Call
    /// <c>AddNativeMcpServer(...)</c> first to register the server and tools.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddNativeMcpApiGatewayHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<McpLambdaHandler>();
        return services;
    }
}
