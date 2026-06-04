using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Native.Mcp.Protocol;

namespace Native.Mcp.Json;

/// <summary>
/// Source-generated <see cref="JsonSerializerContext"/> covering every MCP protocol and
/// envelope type the runtime serializes. Reflection-free and Native AOT compatible.
/// Consumer tool input/output types are serialized via the consumer's own context
/// (passed at registration), not this one.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(JsonRpcRequest))]
[JsonSerializable(typeof(JsonRpcError))]
[JsonSerializable(typeof(McpInitializeResult))]
[JsonSerializable(typeof(McpServerInfo))]
[JsonSerializable(typeof(McpCapabilities))]
[JsonSerializable(typeof(McpToolsCapability))]
[JsonSerializable(typeof(McpToolListResult))]
[JsonSerializable(typeof(McpToolDefinition))]
[JsonSerializable(typeof(McpToolCallResult))]
[JsonSerializable(typeof(McpContent))]
[JsonSerializable(typeof(SwepayProblemDetails))]
[JsonSerializable(typeof(SwepayEnvelopeMetadata))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(JsonNode))]
[JsonSerializable(typeof(JsonObject))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
public sealed partial class McpJsonSerializerContext : JsonSerializerContext;
