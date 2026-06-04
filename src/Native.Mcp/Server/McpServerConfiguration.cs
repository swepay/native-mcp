using Native.Mcp.Protocol;

namespace Native.Mcp;

/// <summary>
/// Immutable server identity and protocol settings, resolved once from
/// <see cref="McpServerOptions"/> at startup and consumed by the dispatcher.
/// </summary>
public sealed class McpServerConfiguration
{
    /// <summary>Initializes a new <see cref="McpServerConfiguration"/>.</summary>
    /// <param name="serverName">The MCP server name.</param>
    /// <param name="serverVersion">The MCP server semantic version.</param>
    /// <param name="protocolVersion">The advertised MCP protocol version.</param>
    public McpServerConfiguration(string serverName, string serverVersion, string protocolVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverName);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(protocolVersion);

        ServerName = serverName;
        ServerVersion = serverVersion;
        ProtocolVersion = protocolVersion;
    }

    /// <summary>The MCP server name (e.g. <c>swepay-native-guard-trial-provisioner</c>).</summary>
    public string ServerName { get; }

    /// <summary>The MCP server semantic version.</summary>
    public string ServerVersion { get; }

    /// <summary>The advertised MCP protocol version (defaults to <see cref="McpProtocol.Version"/>).</summary>
    public string ProtocolVersion { get; }
}
