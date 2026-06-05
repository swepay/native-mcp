namespace Native.Mcp.Telemetry;

/// <summary>
/// Emits operational metrics for the MCP runtime. The default implementation writes
/// CloudWatch Embedded Metric Format (EMF) to stdout; tests can substitute a recorder.
/// </summary>
public interface IMcpMetrics
{
    /// <summary>Records a <c>tools/call</c> outcome.</summary>
    /// <param name="toolName">The tool name.</param>
    /// <param name="success">Whether the envelope reported success.</param>
    /// <param name="durationMs">Wall-clock duration in milliseconds.</param>
    void RecordToolCall(string toolName, bool success, long durationMs);

    /// <summary>Records a <c>tools/list</c> invocation.</summary>
    void RecordToolsList();

    /// <summary>Records an <c>initialize</c> invocation.</summary>
    void RecordInitialize();

    /// <summary>Records a JSON-RPC protocol error.</summary>
    /// <param name="errorCode">The JSON-RPC error code.</param>
    void RecordProtocolError(int errorCode);

    /// <summary>Records an input validation failure for a tool.</summary>
    /// <param name="toolName">The tool name.</param>
    void RecordValidationFailure(string toolName);

    /// <summary>Records a role/permission denial for a tool.</summary>
    /// <param name="toolName">The tool name.</param>
    /// <param name="requiredRole">The role that was required (may be empty if unknown).</param>
    void RecordRoleDenied(string toolName, string requiredRole);
}

/// <summary>A no-op <see cref="IMcpMetrics"/> used when telemetry is not configured.</summary>
public sealed class NoOpMcpMetrics : IMcpMetrics
{
    /// <summary>Singleton instance.</summary>
    public static readonly NoOpMcpMetrics Instance = new();

    private NoOpMcpMetrics()
    {
    }

    /// <inheritdoc/>
    public void RecordToolCall(string toolName, bool success, long durationMs)
    {
    }

    /// <inheritdoc/>
    public void RecordToolsList()
    {
    }

    /// <inheritdoc/>
    public void RecordInitialize()
    {
    }

    /// <inheritdoc/>
    public void RecordProtocolError(int errorCode)
    {
    }

    /// <inheritdoc/>
    public void RecordValidationFailure(string toolName)
    {
    }

    /// <inheritdoc/>
    public void RecordRoleDenied(string toolName, string requiredRole)
    {
    }
}
