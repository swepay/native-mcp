using System.Text.Json;
using Shouldly;
using Native.Mcp.Tests.Fixtures;

namespace Native.Mcp.Tests.Protocol;

/// <summary>
/// Confirms the source generator emitted a valid <c>InputSchemaJson</c> on the tool partials
/// in this assembly (the generator runs against the test project's analyzer reference).
/// </summary>
public sealed class GeneratedSchemaTests
{
    [Fact]
    public void PingTool_InputSchema_IsValidObjectSchema()
    {
        using var doc = JsonDocument.Parse(PingTool.InputSchemaJson);
        doc.RootElement.GetProperty("type").GetString().ShouldBe("object");
    }

    [Fact]
    public void EchoTool_InputSchema_ReflectsAttributesAndRequired()
    {
        using var doc = JsonDocument.Parse(EchoTool.InputSchemaJson);
        var root = doc.RootElement;

        root.GetProperty("$schema").GetString()!.ShouldContain("2020-12");
        root.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ShouldContain("message");

        var message = root.GetProperty("properties").GetProperty("message");
        message.GetProperty("type").GetString().ShouldBe("string");
        message.GetProperty("description").GetString().ShouldBe("The message to echo back.");
        message.GetProperty("maxLength").GetInt32().ShouldBe(100);
        root.GetProperty("additionalProperties").GetBoolean().ShouldBeFalse();
    }
}
