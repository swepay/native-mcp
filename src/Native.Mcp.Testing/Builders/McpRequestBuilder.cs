using System.Text.Json;
using System.Text.Json.Nodes;
using Native.Mcp.Testing.Internal;

namespace Native.Mcp.Testing;

/// <summary>Fluent builder for a raw JSON-RPC 2.0 request body.</summary>
public sealed class McpRequestBuilder
{
    private readonly JsonObject _params = new();
    private string _method = "tools/call";
    private JsonNode? _id = "test-1";
    private bool _hasParams;

    /// <summary>Sets the JSON-RPC method.</summary>
    /// <param name="method">The method name.</param>
    /// <returns>This builder.</returns>
    public McpRequestBuilder WithMethod(string method)
    {
        _method = method;
        return this;
    }

    /// <summary>Sets the JSON-RPC id (string).</summary>
    /// <param name="id">The id.</param>
    /// <returns>This builder.</returns>
    public McpRequestBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    /// <summary>Makes this a notification (no id).</summary>
    /// <returns>This builder.</returns>
    public McpRequestBuilder AsNotification()
    {
        _id = null;
        return this;
    }

    /// <summary>Sets the <c>params.name</c> (tool name) for a <c>tools/call</c>.</summary>
    /// <param name="toolName">The tool name.</param>
    /// <returns>This builder.</returns>
    public McpRequestBuilder WithToolName(string toolName)
    {
        _params["name"] = toolName;
        _hasParams = true;
        return this;
    }

    /// <summary>Sets the <c>params.arguments</c> from an object (serialized reflectively).</summary>
    /// <param name="arguments">The arguments object.</param>
    /// <returns>This builder.</returns>
    public McpRequestBuilder WithArguments(object arguments)
    {
        _params["arguments"] = JsonSerializer.SerializeToNode(arguments, arguments.GetType(), TestJson.Options);
        _hasParams = true;
        return this;
    }

    /// <summary>Builds the JSON-RPC request body string.</summary>
    /// <returns>The request body.</returns>
    public string Build()
    {
        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = _method,
        };

        if (_id is not null)
        {
            request["id"] = _id.DeepClone();
        }

        if (_hasParams)
        {
            request["params"] = _params.DeepClone();
        }

        return request.ToJsonString();
    }
}
