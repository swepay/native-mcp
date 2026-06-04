using System.Text.Json;
using System.Text.Json.Serialization;

namespace Native.Mcp.Protocol;

/// <summary>
/// A JSON-RPC 2.0 request (or notification, when <see cref="Id"/> is absent).
/// <c>Params</c> and <c>Id</c> are kept as raw <see cref="JsonElement"/> because their
/// shape varies by method and the id is echoed back verbatim.
/// </summary>
public sealed class JsonRpcRequest
{
    /// <summary>Protocol marker. Must be <c>"2.0"</c>.</summary>
    [JsonPropertyName("jsonrpc")]
    public string? JsonRpc { get; set; }

    /// <summary>The method name (e.g. <c>initialize</c>, <c>tools/list</c>, <c>tools/call</c>).</summary>
    [JsonPropertyName("method")]
    public string? Method { get; set; }

    /// <summary>Method parameters (method-specific shape).</summary>
    [JsonPropertyName("params")]
    public JsonElement? Params { get; set; }

    /// <summary>Request id. Absent for notifications.</summary>
    [JsonPropertyName("id")]
    public JsonElement? Id { get; set; }
}

/// <summary>
/// A JSON-RPC 2.0 error object, placed under <c>error</c> in a response.
/// </summary>
/// <param name="Code">Numeric error code (e.g. <c>-32601</c> method not found).</param>
/// <param name="Message">Short human-readable message.</param>
/// <param name="Data">Optional structured detail.</param>
public sealed record JsonRpcError(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("data")] JsonElement? Data = null);

/// <summary>
/// Well-known JSON-RPC 2.0 error codes plus the codes this server emits.
/// </summary>
public static class JsonRpcErrorCodes
{
    /// <summary>Invalid JSON was received (-32700).</summary>
    public const int ParseError = -32700;

    /// <summary>The JSON sent is not a valid Request object (-32600).</summary>
    public const int InvalidRequest = -32600;

    /// <summary>The method does not exist (-32601).</summary>
    public const int MethodNotFound = -32601;

    /// <summary>Invalid method parameters, e.g. unknown tool name (-32602).</summary>
    public const int InvalidParams = -32602;

    /// <summary>Internal JSON-RPC error (-32603).</summary>
    public const int InternalError = -32603;
}
