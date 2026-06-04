using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Native.Mcp.Protocol;

namespace Native.Mcp;

/// <summary>
/// Configures an MCP server: identity, protocol version, and the set of registered tools.
/// Populated inside the <c>AddNativeMcpServer(options =&gt; ...)</c> callback.
/// </summary>
public sealed class McpServerOptions
{
    private readonly List<McpToolRegistration> _registrations = [];

    /// <summary>The MCP server name advertised in <c>initialize</c> and envelope metadata.</summary>
    public string ServerName { get; set; } = "mcp-server";

    /// <summary>The MCP server semantic version.</summary>
    public string ServerVersion { get; set; } = "0.0.0";

    /// <summary>The advertised MCP protocol version. Defaults to <see cref="McpProtocol.Version"/>.</summary>
    public string ProtocolVersion { get; set; } = McpProtocol.Version;

    /// <summary>The service lifetime used to register tool implementations. Defaults to scoped.</summary>
    public ServiceLifetime ToolLifetime { get; set; } = ServiceLifetime.Scoped;

    /// <summary>The registrations collected so far.</summary>
    internal IReadOnlyList<McpToolRegistration> Registrations => _registrations;

    /// <summary>
    /// Registers a tool with explicit input/output <see cref="JsonTypeInfo{T}"/> — the
    /// fully Native AOT-safe registration path. Obtain the type infos from your own
    /// source-generated <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>.
    /// </summary>
    /// <typeparam name="TTool">The tool implementation type.</typeparam>
    /// <typeparam name="TInput">The tool input type.</typeparam>
    /// <typeparam name="TOutput">The tool output type.</typeparam>
    /// <param name="inputTypeInfo">JSON type info for the input.</param>
    /// <param name="outputTypeInfo">JSON type info for the output.</param>
    /// <returns>This options instance, for chaining.</returns>
    public McpServerOptions AddTool<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TTool,
        TInput,
        TOutput>(
        JsonTypeInfo<TInput> inputTypeInfo,
        JsonTypeInfo<TOutput> outputTypeInfo)
        where TTool : class, IMcpTool<TInput, TOutput>
        where TInput : class
        where TOutput : class
    {
        ArgumentNullException.ThrowIfNull(inputTypeInfo);
        ArgumentNullException.ThrowIfNull(outputTypeInfo);

        var descriptor = new McpToolDescriptor(
            name: TTool.Name,
            description: TTool.Description,
            inputSchemaJson: TTool.InputSchemaJson,
            toolType: typeof(TTool),
            inputType: typeof(TInput),
            outputType: typeof(TOutput),
            deserializeInput: element => DeserializeInput(element, inputTypeInfo),
            validateInput: static (serviceProvider, input) => ValidateInput<TInput>(serviceProvider, input),
            invoker: (serviceProvider, input, context, cancellationToken) =>
                InvokeAsync<TTool, TInput, TOutput>(serviceProvider, input, context, outputTypeInfo, cancellationToken));

        _registrations.Add(new McpToolRegistration(typeof(TTool), ToolLifetime, descriptor));
        return this;
    }

    /// <summary>
    /// Registers a tool, resolving the input/output type infos from a single
    /// <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>. AOT-safe as long
    /// as the context declares both types via <c>[JsonSerializable]</c>.
    /// </summary>
    /// <typeparam name="TTool">The tool implementation type.</typeparam>
    /// <typeparam name="TInput">The tool input type.</typeparam>
    /// <typeparam name="TOutput">The tool output type.</typeparam>
    /// <param name="context">The serializer context that knows both types.</param>
    /// <returns>This options instance, for chaining.</returns>
    public McpServerOptions AddTool<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TTool,
        TInput,
        TOutput>(
        System.Text.Json.Serialization.JsonSerializerContext context)
        where TTool : class, IMcpTool<TInput, TOutput>
        where TInput : class
        where TOutput : class
    {
        ArgumentNullException.ThrowIfNull(context);

        var inputInfo = (JsonTypeInfo<TInput>?)context.GetTypeInfo(typeof(TInput))
            ?? throw new InvalidOperationException(
                $"The serializer context does not declare a JsonTypeInfo for '{typeof(TInput)}'. " +
                $"Add [JsonSerializable(typeof({typeof(TInput).Name}))] to your context.");
        var outputInfo = (JsonTypeInfo<TOutput>?)context.GetTypeInfo(typeof(TOutput))
            ?? throw new InvalidOperationException(
                $"The serializer context does not declare a JsonTypeInfo for '{typeof(TOutput)}'. " +
                $"Add [JsonSerializable(typeof({typeof(TOutput).Name}))] to your context.");

        return AddTool<TTool, TInput, TOutput>(inputInfo, outputInfo);
    }

    private static object DeserializeInput<TInput>(JsonElement element, JsonTypeInfo<TInput> typeInfo)
        where TInput : class
    {
        var value = element.Deserialize(typeInfo);
        return value ?? throw new JsonException("Tool arguments deserialized to null.");
    }

    private static IReadOnlyList<McpValidationFailure> ValidateInput<TInput>(
        IServiceProvider serviceProvider,
        object input)
        where TInput : class
    {
        var validator = serviceProvider.GetService<Native.FluentValidation.Abstractions.INativeValidator<TInput>>();
        if (validator is null)
        {
            return Array.Empty<McpValidationFailure>();
        }

        var result = validator.Validate((TInput)input);
        if (result.IsValid)
        {
            return Array.Empty<McpValidationFailure>();
        }

        var failures = new List<McpValidationFailure>(result.Errors.Count);
        foreach (var error in result.Errors)
        {
            failures.Add(new McpValidationFailure(error.PropertyName, error.ErrorMessage));
        }

        return failures;
    }

    private static async Task<McpToolInvocationResult> InvokeAsync<TTool, TInput, TOutput>(
        IServiceProvider serviceProvider,
        object input,
        McpExecutionContext context,
        JsonTypeInfo<TOutput> outputTypeInfo,
        CancellationToken cancellationToken)
        where TTool : class, IMcpTool<TInput, TOutput>
        where TInput : class
        where TOutput : class
    {
        var tool = serviceProvider.GetRequiredService<TTool>();
        var result = await tool.ExecuteAsync((TInput)input, context, cancellationToken).ConfigureAwait(false);

        if (result.IsSuccess)
        {
            var data = result.Data;
            return McpToolInvocationResult.Success(
                () => JsonSerializer.SerializeToNode(data, outputTypeInfo));
        }

        return McpToolInvocationResult.Failure(result.Error);
    }
}

/// <summary>Internal record of a single tool registration.</summary>
/// <param name="ToolType">The concrete tool implementation type (for DI registration).</param>
/// <param name="Lifetime">The DI lifetime for the tool.</param>
/// <param name="Descriptor">The runtime descriptor.</param>
internal sealed record McpToolRegistration(
    [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type ToolType,
    ServiceLifetime Lifetime,
    McpToolDescriptor Descriptor);
