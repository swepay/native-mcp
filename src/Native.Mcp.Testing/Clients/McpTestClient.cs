using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Native.Mcp.Protocol;
using Native.Mcp.Testing.Internal;

namespace Native.Mcp.Testing;

/// <summary>
/// Drives an <see cref="McpTestHost"/>'s dispatcher directly (no HTTP), building JSON-RPC
/// requests and parsing the responses into convenient view models.
/// </summary>
public sealed class McpTestClient
{
    private readonly IServiceProvider _serviceProvider;
    private int _requestCounter;

    internal McpTestClient(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>Sends a raw JSON-RPC body and returns the raw dispatch result.</summary>
    /// <param name="body">The request body.</param>
    /// <param name="options">Optional identity/correlation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw response.</returns>
    public async Task<McpRawResponse> SendRawAsync(
        string? body,
        McpRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var context = (options ?? new McpRequestOptions()).ToRequestContext();
        await using var scope = _serviceProvider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<McpJsonRpcDispatcher>();
        var result = await dispatcher.DispatchAsync(body, context, cancellationToken).ConfigureAwait(false);
        return new McpRawResponse(result.HttpStatusCode, result.Body);
    }

    /// <summary>Sends an <c>initialize</c> request.</summary>
    /// <param name="options">Optional identity/correlation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed initialize response.</returns>
    public async Task<McpInitializeResponse> InitializeAsync(
        McpRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var id = ResolveId(options);
        var body = BuildRequest(id, McpProtocol.MethodInitialize, null);
        var raw = await SendRawAsync(body, options, cancellationToken).ConfigureAwait(false);

        var (result, error) = ParseEnvelopeOrError(raw.Body);
        return new McpInitializeResponse
        {
            HttpStatusCode = raw.HttpStatusCode,
            ProtocolError = error,
            ProtocolVersion = result?["protocolVersion"]?.GetValue<string>(),
            ServerName = result?["serverInfo"]?["name"]?.GetValue<string>(),
            ServerVersion = result?["serverInfo"]?["version"]?.GetValue<string>(),
            RawJson = raw.Body,
        };
    }

    /// <summary>Sends a <c>tools/list</c> request.</summary>
    /// <param name="options">Optional identity/correlation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed tools list.</returns>
    public async Task<McpToolsListResponse> ListToolsAsync(
        McpRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var id = ResolveId(options);
        var body = BuildRequest(id, McpProtocol.MethodToolsList, null);
        var raw = await SendRawAsync(body, options, cancellationToken).ConfigureAwait(false);

        var (result, error) = ParseEnvelopeOrError(raw.Body);
        var tools = new List<McpToolListEntry>();
        if (result?["tools"] is JsonArray array)
        {
            foreach (var node in array)
            {
                tools.Add(new McpToolListEntry(
                    node?["name"]?.GetValue<string>() ?? string.Empty,
                    node?["description"]?.GetValue<string>() ?? string.Empty,
                    node?["inputSchema"]?.DeepClone()));
            }
        }

        return new McpToolsListResponse
        {
            HttpStatusCode = raw.HttpStatusCode,
            ProtocolError = error,
            Tools = tools,
            RawJson = raw.Body,
        };
    }

    /// <summary>Sends a <c>tools/call</c> request.</summary>
    /// <param name="toolName">The tool to call.</param>
    /// <param name="arguments">The arguments object (serialized reflectively).</param>
    /// <param name="options">Optional identity/correlation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed tool-call response.</returns>
    public async Task<McpToolCallResponse> CallToolAsync(
        string toolName,
        object? arguments,
        McpRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var id = ResolveId(options);
        var argumentsNode = arguments is null
            ? new JsonObject()
            : JsonSerializer.SerializeToNode(arguments, arguments.GetType(), TestJson.Options);

        var paramsNode = new JsonObject
        {
            ["name"] = toolName,
            ["arguments"] = argumentsNode,
        };

        var body = BuildRequest(id, McpProtocol.MethodToolsCall, paramsNode);
        var raw = await SendRawAsync(body, options, cancellationToken).ConfigureAwait(false);

        var (result, error) = ParseEnvelopeOrError(raw.Body);
        if (error is not null || result is null)
        {
            return new McpToolCallResponse
            {
                HttpStatusCode = raw.HttpStatusCode,
                ProtocolError = error,
                IsError = false,
                Envelope = null,
                RawJson = raw.Body,
            };
        }

        var isError = result["isError"]?.GetValue<bool>() ?? false;
        var envelopeText = result["content"]?[0]?["text"]?.GetValue<string>() ?? "{}";

        return new McpToolCallResponse
        {
            HttpStatusCode = raw.HttpStatusCode,
            ProtocolError = null,
            IsError = isError,
            Envelope = ParseEnvelope(envelopeText),
            RawJson = raw.Body,
        };
    }

    private static McpEnvelopeView ParseEnvelope(string envelopeJson)
    {
        using var document = JsonDocument.Parse(envelopeJson);
        var root = document.RootElement;

        JsonElement? data = root.TryGetProperty("data", out var dataElement)
            && dataElement.ValueKind != JsonValueKind.Null
            ? dataElement.Clone()
            : null;

        SwepayProblemDetails? error = root.TryGetProperty("error", out var errorElement)
            && errorElement.ValueKind == JsonValueKind.Object
            ? errorElement.Deserialize<SwepayProblemDetails>(TestJson.Options)
            : null;

        SwepayEnvelopeMetadata? metadata = root.TryGetProperty("metadata", out var metaElement)
            && metaElement.ValueKind == JsonValueKind.Object
            ? metaElement.Deserialize<SwepayEnvelopeMetadata>(TestJson.Options)
            : null;

        return new McpEnvelopeView
        {
            Success = root.TryGetProperty("success", out var s) && s.GetBoolean(),
            Data = data,
            Error = error,
            Metadata = metadata,
            RawJson = envelopeJson,
        };
    }

    private static (JsonNode? Result, JsonRpcErrorInfo? Error) ParseEnvelopeOrError(string body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return (null, null);
        }

        var root = JsonNode.Parse(body);
        if (root is null)
        {
            return (null, null);
        }

        if (root["error"] is JsonObject errorObject)
        {
            var error = new JsonRpcErrorInfo(
                errorObject["code"]?.GetValue<int>() ?? 0,
                errorObject["message"]?.GetValue<string>() ?? string.Empty,
                errorObject["data"]?.DeepClone());
            return (null, error);
        }

        return (root["result"], null);
    }

    private string ResolveId(McpRequestOptions? options) =>
        options?.Id ?? $"test-{System.Threading.Interlocked.Increment(ref _requestCounter)}";

    private static string BuildRequest(string id, string method, JsonNode? @params)
    {
        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
        };

        if (@params is not null)
        {
            request["params"] = @params;
        }

        return request.ToJsonString();
    }
}
