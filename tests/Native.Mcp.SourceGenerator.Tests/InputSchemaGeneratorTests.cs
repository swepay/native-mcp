using System.Text.Json;
using Shouldly;

namespace Native.Mcp.SourceGenerator.Tests;

/// <summary>
/// Validates the generated JSON Schema (Draft 2020-12) for the five AC4 scenarios. Each test
/// runs the generator over a source snippet, extracts the emitted <c>InputSchemaJson</c>, and
/// asserts it is valid JSON with the expected shape.
/// </summary>
public sealed class InputSchemaGeneratorTests
{
    private static JsonElement Schema(string source, string toolName)
    {
        var json = GeneratorTestDriver.GetSchema(source, toolName);
        json.ShouldNotBeNull($"the generator should emit InputSchemaJson for {toolName}");
        var document = JsonDocument.Parse(json!);
        document.RootElement.GetProperty("$schema").GetString()!.ShouldContain("2020-12");
        document.RootElement.GetProperty("type").GetString().ShouldBe("object");
        return document.RootElement.Clone();
    }

    [Fact]
    public void SimpleInput_ThreePrimitiveProperties()
    {
        const string source = """
        using Native.Mcp;
        namespace Sample;
        public sealed record SimpleInput
        {
            public string Name { get; init; } = "";
            public int Count { get; init; }
            public bool Enabled { get; init; }
        }
        public sealed record SimpleOutput { public string Result { get; init; } = ""; }
        public sealed partial class SimpleTool : IMcpTool<SimpleInput, SimpleOutput>
        {
            public static string Name => "simple";
            public static string Description => "A simple tool.";
            public System.Threading.Tasks.Task<McpToolResult<SimpleOutput>> ExecuteAsync(
                SimpleInput input, McpExecutionContext context, System.Threading.CancellationToken cancellationToken) =>
                System.Threading.Tasks.Task.FromResult(McpToolResult<SimpleOutput>.Success(new SimpleOutput()));
        }
        """;

        var props = Schema(source, "SimpleTool").GetProperty("properties");
        props.GetProperty("name").GetProperty("type").GetString().ShouldBe("string");
        props.GetProperty("count").GetProperty("type").GetString().ShouldBe("integer");
        props.GetProperty("enabled").GetProperty("type").GetString().ShouldBe("boolean");
    }

    [Fact]
    public void EnumInput_EmitsStringEnum()
    {
        const string source = """
        using Native.Mcp;
        namespace Sample;
        public enum Priority { Low, Medium, High }
        public sealed record EnumInput { public Priority Priority { get; init; } }
        public sealed record EnumOutput { public string Result { get; init; } = ""; }
        public sealed partial class EnumTool : IMcpTool<EnumInput, EnumOutput>
        {
            public static string Name => "enum";
            public static string Description => "An enum tool.";
            public System.Threading.Tasks.Task<McpToolResult<EnumOutput>> ExecuteAsync(
                EnumInput input, McpExecutionContext context, System.Threading.CancellationToken cancellationToken) =>
                System.Threading.Tasks.Task.FromResult(McpToolResult<EnumOutput>.Success(new EnumOutput()));
        }
        """;

        var priority = Schema(source, "EnumTool").GetProperty("properties").GetProperty("priority");
        priority.GetProperty("type").GetString().ShouldBe("string");
        priority.GetProperty("enum").EnumerateArray().Select(e => e.GetString())
            .ShouldBe(new[] { "Low", "Medium", "High" });
    }

    [Fact]
    public void NestedRecordInput_InlinesNestedObject()
    {
        const string source = """
        using Native.Mcp;
        namespace Sample;
        public sealed record Address { public string Street { get; init; } = ""; public string City { get; init; } = ""; }
        public sealed record NestedInput { public string Name { get; init; } = ""; public Address HomeAddress { get; init; } = new(); }
        public sealed record NestedOutput { public string Result { get; init; } = ""; }
        public sealed partial class NestedTool : IMcpTool<NestedInput, NestedOutput>
        {
            public static string Name => "nested";
            public static string Description => "A nested tool.";
            public System.Threading.Tasks.Task<McpToolResult<NestedOutput>> ExecuteAsync(
                NestedInput input, McpExecutionContext context, System.Threading.CancellationToken cancellationToken) =>
                System.Threading.Tasks.Task.FromResult(McpToolResult<NestedOutput>.Success(new NestedOutput()));
        }
        """;

        var home = Schema(source, "NestedTool").GetProperty("properties").GetProperty("homeAddress");
        home.GetProperty("type").GetString().ShouldBe("object");
        home.GetProperty("properties").GetProperty("street").GetProperty("type").GetString().ShouldBe("string");
        home.GetProperty("properties").GetProperty("city").GetProperty("type").GetString().ShouldBe("string");
    }

    [Fact]
    public void ArrayOfRecordsInput_EmitsArrayWithObjectItems()
    {
        const string source = """
        using System.Collections.Generic;
        using Native.Mcp;
        namespace Sample;
        public sealed record LineItem { public string Sku { get; init; } = ""; public int Quantity { get; init; } }
        public sealed record ArrayInput { public IReadOnlyList<LineItem> Items { get; init; } = new List<LineItem>(); }
        public sealed record ArrayOutput { public string Result { get; init; } = ""; }
        public sealed partial class ArrayTool : IMcpTool<ArrayInput, ArrayOutput>
        {
            public static string Name => "array";
            public static string Description => "An array tool.";
            public System.Threading.Tasks.Task<McpToolResult<ArrayOutput>> ExecuteAsync(
                ArrayInput input, McpExecutionContext context, System.Threading.CancellationToken cancellationToken) =>
                System.Threading.Tasks.Task.FromResult(McpToolResult<ArrayOutput>.Success(new ArrayOutput()));
        }
        """;

        var items = Schema(source, "ArrayTool").GetProperty("properties").GetProperty("items");
        items.GetProperty("type").GetString().ShouldBe("array");
        var item = items.GetProperty("items");
        item.GetProperty("type").GetString().ShouldBe("object");
        item.GetProperty("properties").GetProperty("sku").GetProperty("type").GetString().ShouldBe("string");
        item.GetProperty("properties").GetProperty("quantity").GetProperty("type").GetString().ShouldBe("integer");
    }

    [Fact]
    public void AllAttributesInput_MapsAttributesToConstraints()
    {
        const string source = """
        using System.ComponentModel;
        using System.ComponentModel.DataAnnotations;
        using Native.Mcp;
        namespace Sample;
        public sealed record AttributesInput
        {
            [Required]
            [RegularExpression(@"^trial_[0-9A-Z]{26}$")]
            [Description("ULID identifier of the trial request.")]
            public required string TrialRequestId { get; init; }

            [Required]
            [MaxLength(200)]
            public required string Company { get; init; }

            [Range(1, 14)]
            [Description("Trial duration in days.")]
            public int TrialDurationDays { get; init; }
        }
        public sealed record AttributesOutput { public string Result { get; init; } = ""; }
        public sealed partial class AttributesTool : IMcpTool<AttributesInput, AttributesOutput>
        {
            public static string Name => "attributes";
            public static string Description => "An attributes tool.";
            public System.Threading.Tasks.Task<McpToolResult<AttributesOutput>> ExecuteAsync(
                AttributesInput input, McpExecutionContext context, System.Threading.CancellationToken cancellationToken) =>
                System.Threading.Tasks.Task.FromResult(McpToolResult<AttributesOutput>.Success(new AttributesOutput()));
        }
        """;

        var root = Schema(source, "AttributesTool");
        var required = root.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToList();
        required.ShouldContain("trialRequestId");
        required.ShouldContain("company");

        var props = root.GetProperty("properties");
        var trialId = props.GetProperty("trialRequestId");
        trialId.GetProperty("description").GetString().ShouldBe("ULID identifier of the trial request.");
        trialId.GetProperty("pattern").GetString().ShouldBe("^trial_[0-9A-Z]{26}$");

        props.GetProperty("company").GetProperty("maxLength").GetInt32().ShouldBe(200);

        var duration = props.GetProperty("trialDurationDays");
        duration.GetProperty("minimum").GetInt32().ShouldBe(1);
        duration.GetProperty("maximum").GetInt32().ShouldBe(14);
    }
}
