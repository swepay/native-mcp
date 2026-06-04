using System.Text.Json;
using System.Text.Json.Nodes;
using Native.Mcp.Json;

namespace Native.Mcp;

/// <summary>
/// Builds the canonical Swepay envelope JSON string. The envelope wraps either success
/// data or an error, plus metadata. Construction uses <see cref="JsonNode"/> composition
/// (rather than serializing a generic <see cref="SwepayEnvelope{T}"/>) so the data node —
/// produced from the tool's own <c>JsonTypeInfo</c> — can be embedded without reflection,
/// keeping the runtime Native AOT compatible.
/// </summary>
public static class SwepayEnvelopeFactory
{
    /// <summary>Builds a success envelope JSON string.</summary>
    /// <param name="dataNode">The pre-serialized success data node (may be null).</param>
    /// <param name="metadata">The envelope metadata.</param>
    /// <returns>The envelope as a compact JSON string.</returns>
    public static string BuildSuccess(JsonNode? dataNode, SwepayEnvelopeMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var obj = new JsonObject
        {
            ["success"] = true,
            ["data"] = dataNode,
            ["error"] = null,
            ["metadata"] = SerializeMetadata(metadata),
        };

        return obj.ToJsonString();
    }

    /// <summary>Builds a failure envelope JSON string.</summary>
    /// <param name="error">The canonical error payload.</param>
    /// <param name="metadata">The envelope metadata.</param>
    /// <returns>The envelope as a compact JSON string.</returns>
    public static string BuildFailure(SwepayProblemDetails error, SwepayEnvelopeMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(metadata);

        var obj = new JsonObject
        {
            ["success"] = false,
            ["data"] = null,
            ["error"] = JsonSerializer.SerializeToNode(error, McpJsonSerializerContext.Default.SwepayProblemDetails),
            ["metadata"] = SerializeMetadata(metadata),
        };

        return obj.ToJsonString();
    }

    private static JsonNode? SerializeMetadata(SwepayEnvelopeMetadata metadata) =>
        JsonSerializer.SerializeToNode(metadata, McpJsonSerializerContext.Default.SwepayEnvelopeMetadata);
}
