using System.Text.Json.Serialization;

namespace Native.Mcp;

/// <summary>
/// The canonical Swepay response envelope, as a typed POCO. The runtime builds the wire
/// JSON via <see cref="SwepayEnvelopeFactory"/> (to support AOT generic data), but this
/// type is provided so consumers and tests can deserialize the envelope strongly.
/// </summary>
/// <typeparam name="T">The success data type.</typeparam>
public sealed class SwepayEnvelope<T>
    where T : class
{
    /// <summary>Whether the operation succeeded.</summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>The success payload, or null on failure.</summary>
    [JsonPropertyName("data")]
    public T? Data { get; set; }

    /// <summary>The error payload, or null on success.</summary>
    [JsonPropertyName("error")]
    public SwepayProblemDetails? Error { get; set; }

    /// <summary>Envelope metadata (always present).</summary>
    [JsonPropertyName("metadata")]
    public SwepayEnvelopeMetadata Metadata { get; set; } = null!;
}

/// <summary>
/// Metadata attached to every Swepay envelope.
/// </summary>
/// <param name="RequestId">JSON-RPC request id.</param>
/// <param name="CorrelationId">Cross-system correlation id.</param>
/// <param name="Timestamp">ISO-8601 UTC timestamp of when the response was produced.</param>
/// <param name="DurationMs">Server-side processing duration in milliseconds.</param>
/// <param name="ServerName">The MCP server name.</param>
/// <param name="ServerVersion">The MCP server semantic version.</param>
public sealed record SwepayEnvelopeMetadata(
    [property: JsonPropertyName("requestId")] string RequestId,
    [property: JsonPropertyName("correlationId")] string CorrelationId,
    [property: JsonPropertyName("timestamp")] string Timestamp,
    [property: JsonPropertyName("durationMs")] long DurationMs,
    [property: JsonPropertyName("serverName")] string ServerName,
    [property: JsonPropertyName("serverVersion")] string ServerVersion);
