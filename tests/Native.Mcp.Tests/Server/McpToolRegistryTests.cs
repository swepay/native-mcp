using System.Text.Json;
using FluentAssertions;

namespace Native.Mcp.Tests.Server;

public sealed class McpToolRegistryTests
{
    private static McpToolDescriptor Descriptor(string name) => new(
        name: name,
        description: $"desc-{name}",
        inputSchemaJson: "{\"type\":\"object\"}",
        toolType: typeof(object),
        inputType: typeof(object),
        outputType: typeof(object),
        deserializeInput: _ => new object(),
        validateInput: (_, _) => System.Array.Empty<McpValidationFailure>(),
        invoker: (_, _, _, _) => Task.FromResult(McpToolInvocationResult.Success(() => null)));

    [Fact]
    public void ListAll_ReturnsDescriptorsInRegistrationOrder()
    {
        var registry = new McpToolRegistry([Descriptor("a"), Descriptor("b"), Descriptor("c")]);

        registry.ListAll().Select(d => d.Name).Should().Equal("a", "b", "c");
    }

    [Fact]
    public void TryGetTool_WhenPresent_ReturnsDescriptor()
    {
        var registry = new McpToolRegistry([Descriptor("ping")]);

        registry.TryGetTool("ping", out var descriptor).Should().BeTrue();
        descriptor!.Name.Should().Be("ping");
    }

    [Fact]
    public void TryGetTool_WhenMissing_ReturnsFalse()
    {
        var registry = new McpToolRegistry([Descriptor("ping")]);

        registry.TryGetTool("nope", out var descriptor).Should().BeFalse();
        descriptor.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithDuplicateNames_Throws()
    {
        var act = () => new McpToolRegistry([Descriptor("dup"), Descriptor("dup")]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*already registered*");
    }
}
