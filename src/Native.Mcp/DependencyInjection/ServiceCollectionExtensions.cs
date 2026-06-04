using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Native.Mcp.Telemetry;
using Native.Mcp.Validation;

namespace Native.Mcp;

/// <summary>
/// Extension methods for registering the Native.Mcp server and telemetry.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers an MCP server: the tool registry, every configured tool, the dispatcher and
    /// the Lambda handler. Telemetry defaults to no-op — call <see cref="AddNativeMcpTelemetry"/>
    /// to emit CloudWatch EMF metrics and structured logs.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures server identity and registers tools.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddNativeMcpServer(
        this IServiceCollection services,
        Action<McpServerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new McpServerOptions();
        configure(options);

        var configuration = new McpServerConfiguration(
            options.ServerName, options.ServerVersion, options.ProtocolVersion);
        services.TryAddSingleton(configuration);

        // Register each tool implementation under its concrete type.
        foreach (var registration in options.Registrations)
        {
            services.TryAdd(ServiceDescriptor.Describe(
                registration.ToolType, registration.ToolType, registration.Lifetime));
        }

        // Build the immutable registry once from the collected descriptors.
        var descriptors = options.Registrations.Select(static r => r.Descriptor).ToArray();
        services.TryAddSingleton<IMcpToolRegistry>(new McpToolRegistry(descriptors));

        // Validation bridge (resolves INativeValidator<T> per tool at call time).
        services.TryAddSingleton<IMcpInputValidator>(NativeMcpInputValidator.Instance);

        // Telemetry defaults: no-op until AddNativeMcpTelemetry replaces them.
        services.TryAddSingleton<IMcpMetrics>(NoOpMcpMetrics.Instance);
        services.TryAddSingleton<IMcpToolLogger>(NoOpMcpToolLogger.Instance);
        services.TryAddSingleton<IMcpTracer>(NoOpMcpTracer.Instance);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ILogger<McpJsonRpcDispatcher>>(NullLogger<McpJsonRpcDispatcher>.Instance);

        services.TryAddScoped<McpJsonRpcDispatcher>();
        services.TryAddSingleton<McpLambdaHandler>();

        return services;
    }

    /// <summary>
    /// Enables MCP telemetry: CloudWatch EMF metrics (<see cref="McpMetrics"/>) and structured
    /// per-call logs (<see cref="McpLogger"/>), both writing to stdout. Replaces the no-op
    /// defaults registered by <see cref="AddNativeMcpServer"/>. Distributed tracing stays a
    /// no-op unless a custom <see cref="IMcpTracer"/> is registered.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddNativeMcpTelemetry(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.Replace(ServiceDescriptor.Singleton<IMcpMetrics>(
            sp => new McpMetrics(writer: null, timeProvider: sp.GetRequiredService<TimeProvider>())));
        services.Replace(ServiceDescriptor.Singleton<IMcpToolLogger>(_ => new McpLogger()));

        return services;
    }
}
