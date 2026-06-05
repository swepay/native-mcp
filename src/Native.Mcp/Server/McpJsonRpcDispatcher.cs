using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Native.Mcp.Json;
using Native.Mcp.Protocol;
using Native.Mcp.Telemetry;
using Native.Mcp.Validation;

namespace Native.Mcp;

/// <summary>
/// Routes a JSON-RPC 2.0 request body to the right MCP method (<c>initialize</c>,
/// <c>tools/list</c>, <c>tools/call</c>) and produces the response body. Transport-agnostic:
/// the API Gateway layer adapts the HTTP API v2 event into <see cref="McpRequestContext"/>
/// and wraps the result back into an HTTP response.
/// </summary>
/// <remarks>
/// Valid JSON-RPC always yields HTTP 200 — tool and validation errors are carried inside the
/// canonical envelope (with <c>isError=true</c>), never as HTTP failures. Notifications yield
/// HTTP 202 with an empty body. Only a malformed request body produces a JSON-RPC
/// <c>-32700 Parse error</c> (still HTTP 200, per the JSON-RPC spec).
/// </remarks>
public sealed class McpJsonRpcDispatcher
{
    private static readonly JsonElement EmptyArguments = CreateEmptyObject();

    private readonly IMcpToolRegistry _registry;
    private readonly McpServerConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly IMcpInputValidator _validator;
    private readonly IMcpMetrics _metrics;
    private readonly IMcpToolLogger _toolLogger;
    private readonly IMcpTracer _tracer;
    private readonly ILogger<McpJsonRpcDispatcher> _logger;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new <see cref="McpJsonRpcDispatcher"/>.</summary>
    /// <param name="registry">The tool registry.</param>
    /// <param name="configuration">Server identity and protocol settings.</param>
    /// <param name="serviceProvider">The (request-scoped) service provider used to resolve tools.</param>
    /// <param name="validator">Input validator (pass-through if validation is not configured).</param>
    /// <param name="metrics">Metrics sink (no-op if telemetry is not configured).</param>
    /// <param name="toolLogger">Structured per-call logger (no-op if not configured).</param>
    /// <param name="tracer">Distributed tracing tracer (no-op if not configured).</param>
    /// <param name="logger">Internal diagnostic logger (for unhandled exceptions).</param>
    /// <param name="timeProvider">Time source (for duration and timestamps).</param>
    public McpJsonRpcDispatcher(
        IMcpToolRegistry registry,
        McpServerConfiguration configuration,
        IServiceProvider serviceProvider,
        IMcpInputValidator validator,
        IMcpMetrics metrics,
        IMcpToolLogger toolLogger,
        IMcpTracer tracer,
        ILogger<McpJsonRpcDispatcher> logger,
        TimeProvider timeProvider)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _toolLogger = toolLogger ?? throw new ArgumentNullException(nameof(toolLogger));
        _tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>Dispatches a raw JSON-RPC request body.</summary>
    /// <param name="rawBody">The request body (JSON text). May be null/empty.</param>
    /// <param name="requestContext">Claims, correlation and idempotency context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The HTTP status and response body.</returns>
    public async Task<McpDispatchResult> DispatchAsync(
        string? rawBody,
        McpRequestContext requestContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestContext);

        JsonRpcRequest? request;
        try
        {
            if (string.IsNullOrWhiteSpace(rawBody))
            {
                return ParseError(null);
            }

            request = JsonSerializer.Deserialize(rawBody, McpJsonSerializerContext.Default.JsonRpcRequest);
        }
        catch (JsonException)
        {
            return ParseError(null);
        }

        if (request is null || string.IsNullOrEmpty(request.Method) || request.JsonRpc != "2.0")
        {
            _metrics.RecordProtocolError(JsonRpcErrorCodes.InvalidRequest);
            return Ok(JsonRpcErrorResponse(
                request?.Id, JsonRpcErrorCodes.InvalidRequest, "Invalid Request", data: null));
        }

        // Notifications (no id) get an empty 202 — MCP Streamable HTTP convention. The server
        // holds no per-connection state, so notifications/initialized is simply acknowledged.
        if (request.Id is null)
        {
            return new McpDispatchResult(202, string.Empty);
        }

        return request.Method switch
        {
            McpProtocol.MethodInitialize => HandleInitialize(request),
            McpProtocol.MethodToolsList => HandleToolsList(request),
            McpProtocol.MethodToolsCall => await HandleToolsCallAsync(request, requestContext, cancellationToken)
                .ConfigureAwait(false),
            _ => HandleMethodNotFound(request),
        };
    }

    private McpDispatchResult HandleInitialize(JsonRpcRequest request)
    {
        _metrics.RecordInitialize();

        var result = new McpInitializeResult(
            ProtocolVersion: _configuration.ProtocolVersion,
            Capabilities: new McpCapabilities(new McpToolsCapability()),
            ServerInfo: new McpServerInfo(_configuration.ServerName, _configuration.ServerVersion));

        var node = JsonSerializer.SerializeToNode(result, McpJsonSerializerContext.Default.McpInitializeResult)!;
        return Ok(JsonRpcResultResponse(request.Id, node));
    }

    private McpDispatchResult HandleToolsList(JsonRpcRequest request)
    {
        _metrics.RecordToolsList();

        var descriptors = _registry.ListAll();
        var definitions = new List<McpToolDefinition>(descriptors.Count);
        foreach (var descriptor in descriptors)
        {
            var schema = JsonNode.Parse(descriptor.InputSchemaJson)
                ?? new JsonObject { ["type"] = "object" };
            definitions.Add(new McpToolDefinition(descriptor.Name, descriptor.Description, schema));
        }

        var result = new McpToolListResult(definitions);
        var node = JsonSerializer.SerializeToNode(result, McpJsonSerializerContext.Default.McpToolListResult)!;
        return Ok(JsonRpcResultResponse(request.Id, node));
    }

    private McpDispatchResult HandleMethodNotFound(JsonRpcRequest request)
    {
        _metrics.RecordProtocolError(JsonRpcErrorCodes.MethodNotFound);
        return Ok(JsonRpcErrorResponse(
            request.Id, JsonRpcErrorCodes.MethodNotFound, "Method not found", data: null));
    }

    private async Task<McpDispatchResult> HandleToolsCallAsync(
        JsonRpcRequest request,
        McpRequestContext requestContext,
        CancellationToken cancellationToken)
    {
        var startedAt = _timeProvider.GetUtcNow();

        if (request.Params is not { ValueKind: JsonValueKind.Object } parameters
            || !parameters.TryGetProperty("name", out var nameElement)
            || nameElement.ValueKind != JsonValueKind.String
            || nameElement.GetString() is not { Length: > 0 } toolName)
        {
            _metrics.RecordProtocolError(JsonRpcErrorCodes.InvalidParams);
            return Ok(JsonRpcErrorResponse(
                request.Id, JsonRpcErrorCodes.InvalidParams, "Missing or invalid 'name' parameter", data: null));
        }

        if (!_registry.TryGetTool(toolName, out var descriptor))
        {
            _metrics.RecordProtocolError(JsonRpcErrorCodes.InvalidParams);
            var data = new JsonObject { ["tool"] = toolName };
            return Ok(JsonRpcErrorResponse(
                request.Id, JsonRpcErrorCodes.InvalidParams, "Tool not found", data));
        }

        var requestId = ResolveRequestId(request.Id);
        var correlationId = string.IsNullOrWhiteSpace(requestContext.CorrelationId)
            ? Guid.NewGuid().ToString("N")
            : requestContext.CorrelationId;

        var context = new McpExecutionContext(
            toolName: toolName,
            requestId: requestId,
            correlationId: correlationId,
            claims: requestContext.Claims,
            rawJwt: requestContext.RawJwt,
            startedAt: startedAt,
            idempotencyKey: requestContext.IdempotencyKey);

        var arguments = parameters.TryGetProperty("arguments", out var argsElement)
            ? argsElement
            : EmptyArguments;

        // 1. Deserialize input.
        object input;
        try
        {
            input = descriptor.DeserializeInput(arguments);
        }
        catch (JsonException)
        {
            _metrics.RecordValidationFailure(toolName);
            var problem = McpProblems.ValidationFailed(
                "The 'arguments' payload does not match the tool's input schema.", requestId);
            return FailToolCall(request.Id, problem, context, startedAt, outcome: "validation_failed");
        }

        // 2. Validate input.
        var validationProblem = _validator.Validate(descriptor, input, _serviceProvider, context);
        if (validationProblem is not null)
        {
            _metrics.RecordValidationFailure(toolName);
            return FailToolCall(request.Id, validationProblem, context, startedAt, outcome: "validation_failed");
        }

        // 3. Execute the tool.
        McpToolInvocationResult invocation;
        try
        {
            using (_tracer.BeginSubsegment(McpTraceSegments.ToolExecute))
            {
                invocation = await descriptor.Invoker(_serviceProvider, input, context, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (McpForbiddenException forbidden)
        {
            // A RequireRole guard tripped: map to a canonical forbidden envelope (not internal-error).
            _metrics.RecordRoleDenied(toolName, forbidden.RequiredRole);
            _metrics.RecordToolCall(toolName, success: false, DurationMs(startedAt));
            var problem = McpProblems.Forbidden(forbidden.Message, requestId);
            return FailToolCall(request.Id, problem, context, startedAt, outcome: "forbidden");
        }
        catch (Exception ex)
        {
            // Bug in the tool: log internally (with details), return a leak-free envelope.
            _logger.LogError(
                ex,
                "Unhandled exception executing tool {ToolName} (correlationId={CorrelationId})",
                toolName,
                correlationId);

            _metrics.RecordToolCall(toolName, success: false, DurationMs(startedAt));
            var problem = McpErrorMapper.MapException(ex, requestId);
            return FailToolCall(request.Id, problem, context, startedAt, outcome: "error");
        }

        if (!invocation.IsSuccess)
        {
            var error = invocation.Error!;
            var isForbidden = string.Equals(error.Type, ProblemTypes.Forbidden, StringComparison.Ordinal);
            if (isForbidden)
            {
                _metrics.RecordRoleDenied(toolName, error.Detail);
            }

            _metrics.RecordToolCall(toolName, success: false, DurationMs(startedAt));
            return FailToolCall(
                request.Id,
                EchoRequestId(error, requestId),
                context,
                startedAt,
                outcome: isForbidden ? "forbidden" : "tool_failure");
        }

        // 4. Success: serialize data via the tool's own JsonTypeInfo.
        var dataNode = invocation.SerializeData!();
        var metadata = BuildMetadata(context, startedAt);
        var envelope = SwepayEnvelopeFactory.BuildSuccess(dataNode, metadata);

        _metrics.RecordToolCall(toolName, success: true, metadata.DurationMs);
        _toolLogger.LogToolExecuted(context, outcome: "success", metadata.DurationMs, envelopeSuccess: true);
        return Ok(BuildToolCallResponse(request.Id, envelope, isError: false));
    }

    private McpDispatchResult FailToolCall(
        JsonElement? id,
        SwepayProblemDetails problem,
        McpExecutionContext context,
        DateTimeOffset startedAt,
        string outcome)
    {
        var metadata = BuildMetadata(context, startedAt);
        _toolLogger.LogToolExecuted(context, outcome, metadata.DurationMs, envelopeSuccess: false);
        var envelope = SwepayEnvelopeFactory.BuildFailure(problem, metadata);
        return Ok(BuildToolCallResponse(id, envelope, isError: true));
    }

    private string BuildToolCallResponse(JsonElement? id, string envelopeJson, bool isError)
    {
        var callResult = new McpToolCallResult([McpContent.FromText(envelopeJson)], isError);
        var node = JsonSerializer.SerializeToNode(callResult, McpJsonSerializerContext.Default.McpToolCallResult)!;
        return JsonRpcResultResponse(id, node);
    }

    private SwepayEnvelopeMetadata BuildMetadata(McpExecutionContext context, DateTimeOffset startedAt)
    {
        var now = _timeProvider.GetUtcNow();
        var durationMs = (long)Math.Max(0, (now - startedAt).TotalMilliseconds);
        return new SwepayEnvelopeMetadata(
            RequestId: context.RequestId,
            CorrelationId: context.CorrelationId,
            Timestamp: now.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture),
            DurationMs: durationMs,
            ServerName: _configuration.ServerName,
            ServerVersion: _configuration.ServerVersion);
    }

    private long DurationMs(DateTimeOffset startedAt) =>
        (long)Math.Max(0, (_timeProvider.GetUtcNow() - startedAt).TotalMilliseconds);

    private static SwepayProblemDetails EchoRequestId(SwepayProblemDetails problem, string requestId) =>
        string.IsNullOrEmpty(problem.RequestId) ? problem with { RequestId = requestId } : problem;

    private McpDispatchResult ParseError(JsonElement? id)
    {
        _metrics.RecordProtocolError(JsonRpcErrorCodes.ParseError);
        return Ok(JsonRpcErrorResponse(id, JsonRpcErrorCodes.ParseError, "Parse error", data: null));
    }

    private static McpDispatchResult Ok(string body) => new(200, body);

    private static string JsonRpcResultResponse(JsonElement? id, JsonNode resultNode)
    {
        var obj = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = EchoId(id),
            ["result"] = resultNode,
        };
        return obj.ToJsonString();
    }

    private static string JsonRpcErrorResponse(JsonElement? id, int code, string message, JsonNode? data)
    {
        var error = new JsonObject
        {
            ["code"] = code,
            ["message"] = message,
        };
        if (data is not null)
        {
            error["data"] = data;
        }

        var obj = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = EchoId(id),
            ["error"] = error,
        };
        return obj.ToJsonString();
    }

    private static JsonNode? EchoId(JsonElement? id) =>
        id is { } value && value.ValueKind != JsonValueKind.Null && value.ValueKind != JsonValueKind.Undefined
            ? JsonNode.Parse(value.GetRawText())
            : null;

    private static string ResolveRequestId(JsonElement? id)
    {
        if (id is not { } value)
        {
            return string.Empty;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            _ => value.GetRawText(),
        };
    }

    private static JsonElement CreateEmptyObject()
    {
        using var document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }
}
