using System.Text.Json;
using System.Text.Json.Nodes;
using Native.Mcp.Testing.Internal;

namespace Native.Mcp.Testing;

/// <summary>A raw dispatch result: HTTP status and JSON body.</summary>
/// <param name="HttpStatusCode">The HTTP status returned by the dispatcher.</param>
/// <param name="Body">The JSON-RPC response body (empty for notifications).</param>
public sealed record McpRawResponse(int HttpStatusCode, string Body);

/// <summary>A JSON-RPC error object parsed from a response.</summary>
/// <param name="Code">The numeric error code.</param>
/// <param name="Message">The error message.</param>
/// <param name="Data">Optional structured data.</param>
public sealed record JsonRpcErrorInfo(int Code, string Message, JsonNode? Data);

/// <summary>Parsed result of an <c>initialize</c> call.</summary>
public sealed class McpInitializeResponse
{
    /// <summary>The HTTP status.</summary>
    public required int HttpStatusCode { get; init; }

    /// <summary>The protocol error, if the call failed at the protocol layer.</summary>
    public JsonRpcErrorInfo? ProtocolError { get; init; }

    /// <summary>The negotiated protocol version.</summary>
    public string? ProtocolVersion { get; init; }

    /// <summary>The server name.</summary>
    public string? ServerName { get; init; }

    /// <summary>The server version.</summary>
    public string? ServerVersion { get; init; }

    /// <summary>The raw response body.</summary>
    public required string RawJson { get; init; }
}

/// <summary>A single tool entry from <c>tools/list</c>.</summary>
/// <param name="Name">Tool name.</param>
/// <param name="Description">Tool description.</param>
/// <param name="InputSchema">The tool's input JSON Schema.</param>
public sealed record McpToolListEntry(string Name, string Description, JsonNode? InputSchema);

/// <summary>Parsed result of a <c>tools/list</c> call.</summary>
public sealed class McpToolsListResponse
{
    /// <summary>The HTTP status.</summary>
    public required int HttpStatusCode { get; init; }

    /// <summary>The protocol error, if any.</summary>
    public JsonRpcErrorInfo? ProtocolError { get; init; }

    /// <summary>The listed tools.</summary>
    public required IReadOnlyList<McpToolListEntry> Tools { get; init; }

    /// <summary>The raw response body.</summary>
    public required string RawJson { get; init; }
}

/// <summary>A parsed view over the canonical Swepay envelope.</summary>
public sealed class McpEnvelopeView
{
    /// <summary>Whether the envelope reported success.</summary>
    public bool Success { get; init; }

    /// <summary>The success data (raw), if present.</summary>
    public JsonElement? Data { get; init; }

    /// <summary>The error payload, if present.</summary>
    public SwepayProblemDetails? Error { get; init; }

    /// <summary>The envelope metadata.</summary>
    public SwepayEnvelopeMetadata? Metadata { get; init; }

    /// <summary>The raw envelope JSON.</summary>
    public required string RawJson { get; init; }

    /// <summary>Deserializes the data into <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <returns>The deserialized data, or default if absent.</returns>
    public T? DataAs<T>() => Data is { } data ? data.Deserialize<T>(TestJson.Options) : default;
}

/// <summary>Parsed result of a <c>tools/call</c> call.</summary>
public sealed class McpToolCallResponse
{
    /// <summary>The HTTP status.</summary>
    public required int HttpStatusCode { get; init; }

    /// <summary>The protocol error, if the call failed at the JSON-RPC layer (e.g. unknown tool).</summary>
    public JsonRpcErrorInfo? ProtocolError { get; init; }

    /// <summary>Whether the MCP tool result was flagged as an error (<c>result.isError</c>).</summary>
    public bool IsError { get; init; }

    /// <summary>The parsed envelope (null only when a protocol error occurred).</summary>
    public McpEnvelopeView? Envelope { get; init; }

    /// <summary>The raw response body.</summary>
    public required string RawJson { get; init; }
}
