using System.Text.Json.Serialization;

namespace Native.Mcp.Tests.Fixtures;

/// <summary>
/// AOT-style serializer context for the test tools — exercises the explicit
/// <c>JsonTypeInfo</c> registration path and the generated <c>AddDiscoveredTools</c>.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PingInput))]
[JsonSerializable(typeof(PingOutput))]
[JsonSerializable(typeof(EchoInput))]
[JsonSerializable(typeof(EchoOutput))]
[JsonSerializable(typeof(FailingInput))]
[JsonSerializable(typeof(FailingOutput))]
[JsonSerializable(typeof(ScopedInput))]
[JsonSerializable(typeof(ScopedOutput))]
public sealed partial class TestToolsJsonContext : JsonSerializerContext;
