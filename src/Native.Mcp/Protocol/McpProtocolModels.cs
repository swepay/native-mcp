using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Native.Mcp.Protocol;

/// <summary>Result of the <c>initialize</c> handshake.</summary>
/// <param name="ProtocolVersion">Negotiated MCP protocol version.</param>
/// <param name="Capabilities">Server capabilities advertised to the client.</param>
/// <param name="ServerInfo">Server identity.</param>
public sealed record McpInitializeResult(
    [property: JsonPropertyName("protocolVersion")] string ProtocolVersion,
    [property: JsonPropertyName("capabilities")] McpCapabilities Capabilities,
    [property: JsonPropertyName("serverInfo")] McpServerInfo ServerInfo);

/// <summary>Server identity returned by <c>initialize</c>.</summary>
/// <param name="Name">Server name.</param>
/// <param name="Version">Server semantic version.</param>
public sealed record McpServerInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version);

/// <summary>Capabilities advertised by the server. v0 advertises only <c>tools</c>.</summary>
/// <param name="Tools">Tools capability marker (empty object).</param>
public sealed record McpCapabilities(
    [property: JsonPropertyName("tools")] McpToolsCapability Tools);

/// <summary>Marker object for the tools capability. Carries no fields in v0.</summary>
public sealed record McpToolsCapability;

/// <summary>Result of <c>tools/list</c>.</summary>
/// <param name="Tools">The advertised tools.</param>
public sealed record McpToolListResult(
    [property: JsonPropertyName("tools")] IReadOnlyList<McpToolDefinition> Tools);

/// <summary>A single tool entry in <c>tools/list</c>.</summary>
/// <param name="Name">Unique tool name.</param>
/// <param name="Description">Human-readable description.</param>
/// <param name="InputSchema">JSON Schema (Draft 2020-12) for the tool input.</param>
public sealed record McpToolDefinition(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("inputSchema")] JsonNode InputSchema);

/// <summary>Result of <c>tools/call</c>. The Swepay envelope is serialized into the
/// single text content block; <see cref="IsError"/> mirrors <c>!envelope.success</c>.</summary>
/// <param name="Content">Content blocks. Always one text block in v0.</param>
/// <param name="IsError">Whether the tool result represents an error.</param>
public sealed record McpToolCallResult(
    [property: JsonPropertyName("content")] IReadOnlyList<McpContent> Content,
    [property: JsonPropertyName("isError")] bool IsError);

/// <summary>A content block within a tool result. v0 only emits <c>type=text</c>.</summary>
/// <param name="Type">Content type discriminator.</param>
/// <param name="Text">The text payload (the serialized Swepay envelope).</param>
public sealed record McpContent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] string Text)
{
    /// <summary>Creates a <c>text</c> content block.</summary>
    /// <param name="text">The text payload.</param>
    /// <returns>A text <see cref="McpContent"/>.</returns>
    public static McpContent FromText(string text) => new("text", text);
}
