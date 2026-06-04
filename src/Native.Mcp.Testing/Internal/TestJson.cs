using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Native.Mcp.Testing.Internal;

/// <summary>
/// Shared reflection-based JSON options for the testing library. Tests are not subject to the
/// AOT constraints of the runtime, so a reflection resolver is used for ergonomic, schema-less
/// (de)serialization of arbitrary tool input/output types.
/// </summary>
internal static class TestJson
{
    /// <summary>Web defaults (camelCase) with a reflection type resolver.</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    public static JsonTypeInfo GetTypeInfo(Type type) => Options.GetTypeInfo(type);
}
