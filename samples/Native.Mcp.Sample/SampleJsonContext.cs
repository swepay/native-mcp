using System.Text.Json.Serialization;
using Amazon.Lambda.APIGatewayEvents;

namespace Native.Mcp.Sample;

/// <summary>
/// Source-generated serializer context for the sample. Covers both the Lambda event types
/// (for the runtime serializer) and the tool input/output types (for AOT-safe registration
/// via <c>AddDiscoveredTools</c>).
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest))]
[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyResponse))]
[JsonSerializable(typeof(PingInput))]
[JsonSerializable(typeof(PingOutput))]
[JsonSerializable(typeof(EchoInput))]
[JsonSerializable(typeof(EchoOutput))]
public sealed partial class SampleJsonContext : JsonSerializerContext;
