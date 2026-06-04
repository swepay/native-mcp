namespace Native.Mcp.Telemetry;

/// <summary>
/// Emits a structured, end-of-call log record for each <c>tools/call</c>. The default
/// implementation writes JSON lines; tests can capture records. Implementations must never
/// log the raw JWT, claim values that identify a person (email, sub), or exception text.
/// </summary>
public interface IMcpToolLogger
{
    /// <summary>Logs the outcome of a tool execution.</summary>
    /// <param name="context">The execution context.</param>
    /// <param name="outcome">A coarse outcome label (e.g. <c>success</c>, <c>validation_failed</c>, <c>error</c>).</param>
    /// <param name="durationMs">Wall-clock duration in milliseconds.</param>
    /// <param name="envelopeSuccess">Whether the canonical envelope reported success.</param>
    void LogToolExecuted(McpExecutionContext context, string outcome, long durationMs, bool envelopeSuccess);
}

/// <summary>A no-op <see cref="IMcpToolLogger"/> used when structured logging is not configured.</summary>
public sealed class NoOpMcpToolLogger : IMcpToolLogger
{
    /// <summary>Singleton instance.</summary>
    public static readonly NoOpMcpToolLogger Instance = new();

    private NoOpMcpToolLogger()
    {
    }

    /// <inheritdoc/>
    public void LogToolExecuted(McpExecutionContext context, string outcome, long durationMs, bool envelopeSuccess)
    {
    }
}
