using System.Text.Json.Nodes;

namespace Native.Mcp.Telemetry;

/// <summary>
/// <see cref="IMcpMetrics"/> implementation that writes CloudWatch Embedded Metric Format
/// (EMF) JSON lines to a writer (stdout in Lambda). CloudWatch parses these lines into
/// metrics automatically. Reflection-free and Native AOT compatible.
/// </summary>
public sealed class McpMetrics : IMcpMetrics
{
    /// <summary>The default CloudWatch namespace for MCP metrics.</summary>
    public const string DefaultNamespace = "Swepay/Mcp";

    private const string UnitCount = "Count";
    private const string UnitMilliseconds = "Milliseconds";

    private readonly TextWriter _writer;
    private readonly TimeProvider _timeProvider;
    private readonly string _namespace;
    private readonly object _gate = new();

    /// <summary>Initializes a new <see cref="McpMetrics"/>.</summary>
    /// <param name="writer">Destination writer. Defaults to <see cref="Console.Out"/>.</param>
    /// <param name="timeProvider">Time source for the EMF timestamp.</param>
    /// <param name="metricNamespace">CloudWatch namespace. Defaults to <see cref="DefaultNamespace"/>.</param>
    public McpMetrics(TextWriter? writer = null, TimeProvider? timeProvider = null, string? metricNamespace = null)
    {
        _writer = writer ?? Console.Out;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _namespace = string.IsNullOrWhiteSpace(metricNamespace) ? DefaultNamespace : metricNamespace;
    }

    /// <inheritdoc/>
    public void RecordToolCall(string toolName, bool success, long durationMs)
    {
        var root = new JsonObject
        {
            ["tool_name"] = toolName,
            ["success"] = success ? "true" : "false",
            ["mcp.tools.call.count"] = 1,
            ["mcp.tools.call.duration_ms"] = durationMs,
        };
        Emit(root, ["tool_name", "success"],
            (Name: "mcp.tools.call.count", Unit: UnitCount),
            (Name: "mcp.tools.call.duration_ms", Unit: UnitMilliseconds));
    }

    /// <inheritdoc/>
    public void RecordToolsList()
    {
        var root = new JsonObject { ["mcp.tools.list.count"] = 1 };
        Emit(root, [], (Name: "mcp.tools.list.count", Unit: UnitCount));
    }

    /// <inheritdoc/>
    public void RecordInitialize()
    {
        var root = new JsonObject { ["mcp.initialize.count"] = 1 };
        Emit(root, [], (Name: "mcp.initialize.count", Unit: UnitCount));
    }

    /// <inheritdoc/>
    public void RecordProtocolError(int errorCode)
    {
        var root = new JsonObject
        {
            ["error_code"] = errorCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["mcp.protocol.errors.count"] = 1,
        };
        Emit(root, ["error_code"], (Name: "mcp.protocol.errors.count", Unit: UnitCount));
    }

    /// <inheritdoc/>
    public void RecordValidationFailure(string toolName)
    {
        var root = new JsonObject
        {
            ["tool_name"] = toolName,
            ["mcp.validation.failures.count"] = 1,
        };
        Emit(root, ["tool_name"], (Name: "mcp.validation.failures.count", Unit: UnitCount));
    }

    /// <inheritdoc/>
    public void RecordScopeDenied(string toolName, string requiredScope)
    {
        var root = new JsonObject
        {
            ["tool_name"] = toolName,
            ["required_scope"] = requiredScope,
            ["mcp.scope.denied.count"] = 1,
        };
        Emit(root, ["tool_name", "required_scope"], (Name: "mcp.scope.denied.count", Unit: UnitCount));
    }

    private void Emit(JsonObject root, string[] dimensionKeys, params (string Name, string Unit)[] metrics)
    {
        // Build via the params-array constructor (AOT-safe) rather than JsonArray.Add<T>.
        var metricDefs = new JsonNode?[metrics.Length];
        for (var i = 0; i < metrics.Length; i++)
        {
            metricDefs[i] = new JsonObject { ["Name"] = metrics[i].Name, ["Unit"] = metrics[i].Unit };
        }

        var dimensionNodes = new JsonNode?[dimensionKeys.Length];
        for (var i = 0; i < dimensionKeys.Length; i++)
        {
            dimensionNodes[i] = JsonValue.Create(dimensionKeys[i]);
        }

        var cloudWatchMetric = new JsonObject
        {
            ["Namespace"] = _namespace,
            ["Dimensions"] = new JsonArray(new JsonArray(dimensionNodes)),
            ["Metrics"] = new JsonArray(metricDefs),
        };

        root["_aws"] = new JsonObject
        {
            ["Timestamp"] = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds(),
            ["CloudWatchMetrics"] = new JsonArray(cloudWatchMetric),
        };

        var line = root.ToJsonString();
        lock (_gate)
        {
            _writer.WriteLine(line);
        }
    }
}
