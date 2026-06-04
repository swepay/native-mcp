namespace Native.Mcp;

/// <summary>
/// Runtime metadata describing a registered tool. Built once at registration time and
/// reused for every <c>tools/list</c> / <c>tools/call</c>. The <see cref="Invoker"/> is
/// a strongly-typed, reflection-free delegate captured when the tool is registered, so
/// dispatch stays AOT-safe.
/// </summary>
public sealed class McpToolDescriptor
{
    /// <summary>Initializes a new <see cref="McpToolDescriptor"/>.</summary>
    /// <param name="name">Unique tool name.</param>
    /// <param name="description">Human-readable description.</param>
    /// <param name="inputSchemaJson">Generated JSON Schema for the input type.</param>
    /// <param name="toolType">The concrete tool implementation type.</param>
    /// <param name="inputType">The tool input type.</param>
    /// <param name="outputType">The tool output type.</param>
    /// <param name="deserializeInput">Deserializes the raw arguments JSON into the input type.</param>
    /// <param name="validateInput">Resolves a validator for the input type (if any) and validates.</param>
    /// <param name="invoker">Resolves the tool and executes it, returning the serialized envelope payload.</param>
    public McpToolDescriptor(
        string name,
        string description,
        string inputSchemaJson,
        Type toolType,
        Type inputType,
        Type outputType,
        Func<System.Text.Json.JsonElement, object> deserializeInput,
        Func<IServiceProvider, object, IReadOnlyList<McpValidationFailure>> validateInput,
        McpToolInvoker invoker)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        InputSchemaJson = inputSchemaJson ?? throw new ArgumentNullException(nameof(inputSchemaJson));
        ToolType = toolType ?? throw new ArgumentNullException(nameof(toolType));
        InputType = inputType ?? throw new ArgumentNullException(nameof(inputType));
        OutputType = outputType ?? throw new ArgumentNullException(nameof(outputType));
        DeserializeInput = deserializeInput ?? throw new ArgumentNullException(nameof(deserializeInput));
        ValidateInput = validateInput ?? throw new ArgumentNullException(nameof(validateInput));
        Invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
    }

    /// <summary>Unique tool name.</summary>
    public string Name { get; }

    /// <summary>Human-readable description (shown in <c>tools/list</c>).</summary>
    public string Description { get; }

    /// <summary>JSON Schema (Draft 2020-12) for the input type.</summary>
    public string InputSchemaJson { get; }

    /// <summary>The concrete tool implementation type.</summary>
    public Type ToolType { get; }

    /// <summary>The tool input type.</summary>
    public Type InputType { get; }

    /// <summary>The tool output type.</summary>
    public Type OutputType { get; }

    /// <summary>
    /// Deserializes a raw <c>arguments</c> JSON element into a boxed instance of the
    /// input type, using the consumer-supplied <c>JsonTypeInfo</c> (AOT-safe).
    /// </summary>
    public Func<System.Text.Json.JsonElement, object> DeserializeInput { get; }

    /// <summary>
    /// Resolves an <c>INativeValidator&lt;TInput&gt;</c> from the service provider (if one is
    /// registered) and validates the input. Returns an empty list when valid or when no
    /// validator is registered. Captured generically at registration, so it needs no reflection.
    /// </summary>
    public Func<IServiceProvider, object, IReadOnlyList<McpValidationFailure>> ValidateInput { get; }

    /// <summary>
    /// Resolves the tool from the service provider and executes it. Returns the boxed
    /// <see cref="McpToolInvocationResult"/> describing success/failure.
    /// </summary>
    public McpToolInvoker Invoker { get; }
}

/// <summary>A single input validation failure, normalized away from any validator library.</summary>
/// <param name="PropertyName">The offending property name.</param>
/// <param name="Message">A human-readable, end-user-safe message.</param>
public sealed record McpValidationFailure(string PropertyName, string Message);

/// <summary>
/// Strongly-typed tool invocation delegate. Captures the tool's input/output types at
/// registration so dispatch needs no reflection.
/// </summary>
/// <param name="serviceProvider">The request-scoped service provider.</param>
/// <param name="input">The deserialized input (boxed).</param>
/// <param name="context">The execution context.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>The invocation result (success data node factory or error).</returns>
public delegate Task<McpToolInvocationResult> McpToolInvoker(
    IServiceProvider serviceProvider,
    object input,
    McpExecutionContext context,
    CancellationToken cancellationToken);

/// <summary>
/// Outcome of invoking a tool, normalized away from the generic
/// <see cref="McpToolResult{T}"/> so the dispatcher can handle all tools uniformly.
/// On success, <see cref="SerializeData"/> produces the <c>data</c> JSON node using the
/// tool's output <c>JsonTypeInfo</c>.
/// </summary>
public sealed class McpToolInvocationResult
{
    private McpToolInvocationResult(
        bool isSuccess,
        Func<System.Text.Json.Nodes.JsonNode?>? serializeData,
        SwepayProblemDetails? error)
    {
        IsSuccess = isSuccess;
        SerializeData = serializeData;
        Error = error;
    }

    /// <summary>Whether the tool succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Produces the success <c>data</c> JSON node (AOT-safe), or null on failure.</summary>
    public Func<System.Text.Json.Nodes.JsonNode?>? SerializeData { get; }

    /// <summary>The error payload, or null on success.</summary>
    public SwepayProblemDetails? Error { get; }

    /// <summary>Creates a successful invocation result.</summary>
    /// <param name="serializeData">Factory that serializes the data to a JSON node.</param>
    /// <returns>A successful <see cref="McpToolInvocationResult"/>.</returns>
    public static McpToolInvocationResult Success(Func<System.Text.Json.Nodes.JsonNode?> serializeData) =>
        new(isSuccess: true, serializeData: serializeData, error: null);

    /// <summary>Creates a failed invocation result.</summary>
    /// <param name="error">The error payload.</param>
    /// <returns>A failed <see cref="McpToolInvocationResult"/>.</returns>
    public static McpToolInvocationResult Failure(SwepayProblemDetails error) =>
        new(isSuccess: false, serializeData: null, error: error);
}
