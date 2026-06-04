using System.Text.Json.Nodes;

namespace Native.Mcp.Telemetry;

/// <summary>
/// Default <see cref="IMcpToolLogger"/>: writes one structured JSON log line per tool call.
/// </summary>
/// <remarks>
/// The record carries correlation id, tool name, a non-PII service-account id (derived from
/// the <c>client_id</c>/<c>azp</c> claim, falling back to <c>sub</c>), outcome, duration and
/// envelope success. It deliberately never includes the raw JWT, email, or exception text.
/// </remarks>
public sealed class McpLogger : IMcpToolLogger
{
    private readonly TextWriter _writer;
    private readonly object _gate = new();

    /// <summary>Initializes a new <see cref="McpLogger"/>.</summary>
    /// <param name="writer">Destination writer. Defaults to <see cref="Console.Out"/>.</param>
    public McpLogger(TextWriter? writer = null)
    {
        _writer = writer ?? Console.Out;
    }

    /// <inheritdoc/>
    public void LogToolExecuted(McpExecutionContext context, string outcome, long durationMs, bool envelopeSuccess)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(outcome);

        var record = new JsonObject
        {
            ["level"] = envelopeSuccess ? "INFO" : "WARN",
            ["message"] = "tool executed",
            ["correlationId"] = context.CorrelationId,
            ["toolName"] = context.ToolName,
            ["serviceAccountId"] = ResolveServiceAccountId(context),
            ["outcome"] = outcome,
            ["durationMs"] = durationMs,
            ["envelopeSuccess"] = envelopeSuccess,
        };

        var line = record.ToJsonString();
        lock (_gate)
        {
            _writer.WriteLine(line);
        }
    }

    private static string ResolveServiceAccountId(McpExecutionContext context)
    {
        if (context.Claims.TryGetValue("client_id", out var clientId) && !string.IsNullOrEmpty(clientId))
        {
            return clientId;
        }

        if (context.Claims.TryGetValue("azp", out var azp) && !string.IsNullOrEmpty(azp))
        {
            return azp;
        }

        // Falls back to subject. Treated as a non-personal service-account id in the M2M model.
        return context.Subject;
    }
}
