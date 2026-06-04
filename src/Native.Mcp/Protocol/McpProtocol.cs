namespace Native.Mcp.Protocol;

/// <summary>Protocol-level constants for the MCP server.</summary>
public static class McpProtocol
{
    /// <summary>The MCP protocol version this server advertises in <c>initialize</c>.</summary>
    public const string Version = "2025-06-18";

    /// <summary>Method name: initialize handshake.</summary>
    public const string MethodInitialize = "initialize";

    /// <summary>Method name: list tools.</summary>
    public const string MethodToolsList = "tools/list";

    /// <summary>Method name: call a tool.</summary>
    public const string MethodToolsCall = "tools/call";

    /// <summary>Notification sent by the client after a successful initialize.</summary>
    public const string NotificationInitialized = "notifications/initialized";
}
