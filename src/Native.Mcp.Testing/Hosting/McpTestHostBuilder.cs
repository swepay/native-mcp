using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Native.Mcp.Telemetry;
using Native.Mcp.Testing.Internal;

namespace Native.Mcp.Testing;

/// <summary>
/// Fluent builder for an in-memory <see cref="McpTestHost"/>. Wires up the MCP server with a
/// real DI container and dispatcher, but without any Lambda or HTTP transport.
/// </summary>
public sealed class McpTestHostBuilder
{
    private readonly ServiceCollection _services = new();
    private readonly List<Action<McpServerOptions>> _toolActions = new();
    private readonly List<Action<IServiceCollection>> _serviceActions = new();
    private string _serverName = "test-mcp-server";
    private string _serverVersion = "0.0.0-test";
    private IMcpMetrics? _metrics;
    private IMcpToolLogger? _toolLogger;
    private TimeProvider? _timeProvider;

    /// <summary>Sets the server name and version advertised by the host.</summary>
    /// <param name="name">Server name.</param>
    /// <param name="version">Server version.</param>
    /// <returns>This builder.</returns>
    public McpTestHostBuilder WithServerInfo(string name, string version)
    {
        _serverName = name;
        _serverVersion = version;
        return this;
    }

    /// <summary>Registers a tool, inferring its input/output types via reflection.</summary>
    /// <typeparam name="TTool">The tool type implementing <see cref="IMcpTool{TInput, TOutput}"/>.</typeparam>
    /// <returns>This builder.</returns>
    public McpTestHostBuilder AddTool<TTool>()
        where TTool : class
    {
        var toolInterface = typeof(TTool).GetInterfaces().FirstOrDefault(static i =>
            i.IsGenericType && i.Namespace == "Native.Mcp" && i.Name == "IMcpTool`2");

        if (toolInterface is null)
        {
            throw new InvalidOperationException(
                $"Type '{typeof(TTool)}' does not implement IMcpTool<TInput, TOutput>.");
        }

        var args = toolInterface.GetGenericArguments();
        _toolActions.Add(options => InvokeAddTool(options, typeof(TTool), args[0], args[1]));
        return this;
    }

    /// <summary>Registers a tool with explicit type infos (no reflection).</summary>
    /// <typeparam name="TTool">The tool type.</typeparam>
    /// <typeparam name="TInput">The input type.</typeparam>
    /// <typeparam name="TOutput">The output type.</typeparam>
    /// <param name="inputTypeInfo">Input type info.</param>
    /// <param name="outputTypeInfo">Output type info.</param>
    /// <returns>This builder.</returns>
    public McpTestHostBuilder AddTool<
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] TTool,
        TInput,
        TOutput>(
        JsonTypeInfo<TInput> inputTypeInfo,
        JsonTypeInfo<TOutput> outputTypeInfo)
        where TTool : class, IMcpTool<TInput, TOutput>
        where TInput : class
        where TOutput : class
    {
        _toolActions.Add(options => options.AddTool<TTool, TInput, TOutput>(inputTypeInfo, outputTypeInfo));
        return this;
    }

    /// <summary>Registers additional services (e.g. tool dependencies).</summary>
    /// <param name="configure">Service configuration.</param>
    /// <returns>This builder.</returns>
    public McpTestHostBuilder ConfigureServices(Action<IServiceCollection> configure)
    {
        _serviceActions.Add(configure);
        return this;
    }

    /// <summary>Overrides the metrics sink (e.g. to capture EMF output in a test).</summary>
    /// <param name="metrics">The metrics implementation.</param>
    /// <returns>This builder.</returns>
    public McpTestHostBuilder WithMetrics(IMcpMetrics metrics)
    {
        _metrics = metrics;
        return this;
    }

    /// <summary>Overrides the structured tool logger (e.g. to capture logs in a test).</summary>
    /// <param name="toolLogger">The logger implementation.</param>
    /// <returns>This builder.</returns>
    public McpTestHostBuilder WithToolLogger(IMcpToolLogger toolLogger)
    {
        _toolLogger = toolLogger;
        return this;
    }

    /// <summary>Overrides the time source (for deterministic durations/timestamps).</summary>
    /// <param name="timeProvider">The time provider.</param>
    /// <returns>This builder.</returns>
    public McpTestHostBuilder WithTimeProvider(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        return this;
    }

    /// <summary>Builds the in-memory host.</summary>
    /// <returns>The <see cref="McpTestHost"/>.</returns>
    public McpTestHost Build()
    {
        foreach (var configure in _serviceActions)
        {
            configure(_services);
        }

        _services.AddNativeMcpServer(options =>
        {
            options.ServerName = _serverName;
            options.ServerVersion = _serverVersion;
            foreach (var toolAction in _toolActions)
            {
                toolAction(options);
            }
        });

        if (_metrics is not null)
        {
            _services.Replace(ServiceDescriptor.Singleton(_metrics));
        }

        if (_toolLogger is not null)
        {
            _services.Replace(ServiceDescriptor.Singleton(_toolLogger));
        }

        if (_timeProvider is not null)
        {
            _services.Replace(ServiceDescriptor.Singleton(_timeProvider));
        }

        var provider = _services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
        });

        return new McpTestHost(provider);
    }

    private static void InvokeAddTool(McpServerOptions options, Type toolType, Type inputType, Type outputType)
    {
        var inputInfo = TestJson.GetTypeInfo(inputType);
        var outputInfo = TestJson.GetTypeInfo(outputType);

        var method = typeof(McpServerOptions).GetMethods()
            .First(m => m.Name == nameof(McpServerOptions.AddTool)
                && m.GetGenericArguments().Length == 3
                && m.GetParameters() is { Length: 2 } p
                && p[0].ParameterType.IsGenericType
                && p[0].ParameterType.GetGenericTypeDefinition() == typeof(JsonTypeInfo<>));

        method.MakeGenericMethod(toolType, inputType, outputType)
            .Invoke(options, new object[] { inputInfo, outputInfo });
    }
}
