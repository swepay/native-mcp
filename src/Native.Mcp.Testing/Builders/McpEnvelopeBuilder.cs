using System.Text.Json;
using System.Text.Json.Nodes;
using Native.Mcp.Testing.Internal;

namespace Native.Mcp.Testing;

/// <summary>Fluent builder for a canonical Swepay envelope JSON string (test fixtures).</summary>
public sealed class McpEnvelopeBuilder
{
    private bool _success = true;
    private JsonNode? _data;
    private SwepayProblemDetails? _error;
    private SwepayEnvelopeMetadata _metadata = new(
        RequestId: "test-request",
        CorrelationId: "test-correlation",
        Timestamp: "2026-06-03T00:00:00.000Z",
        DurationMs: 0,
        ServerName: "test-mcp-server",
        ServerVersion: "0.0.0-test");

    /// <summary>Marks the envelope a success carrying <paramref name="data"/>.</summary>
    /// <param name="data">The success data (serialized reflectively).</param>
    /// <returns>This builder.</returns>
    public McpEnvelopeBuilder AsSuccess(object data)
    {
        _success = true;
        _data = JsonSerializer.SerializeToNode(data, data.GetType(), TestJson.Options);
        _error = null;
        return this;
    }

    /// <summary>Marks the envelope a failure carrying <paramref name="error"/>.</summary>
    /// <param name="error">The error payload.</param>
    /// <returns>This builder.</returns>
    public McpEnvelopeBuilder AsFailure(SwepayProblemDetails error)
    {
        _success = false;
        _error = error;
        _data = null;
        return this;
    }

    /// <summary>Overrides the metadata.</summary>
    /// <param name="metadata">The metadata.</param>
    /// <returns>This builder.</returns>
    public McpEnvelopeBuilder WithMetadata(SwepayEnvelopeMetadata metadata)
    {
        _metadata = metadata;
        return this;
    }

    /// <summary>Builds the envelope JSON string.</summary>
    /// <returns>The envelope JSON.</returns>
    public string Build()
    {
        var metadataNode = JsonSerializer.SerializeToNode(_metadata, TestJson.Options);
        var errorNode = _error is null ? null : JsonSerializer.SerializeToNode(_error, TestJson.Options);

        var root = new JsonObject
        {
            ["success"] = _success,
            ["data"] = _data?.DeepClone(),
            ["error"] = errorNode,
            ["metadata"] = metadataNode,
        };

        return root.ToJsonString();
    }
}
