using System.Text.Json;
using FluentAssertions;

namespace Native.Mcp.SourceGenerator.Tests;

/// <summary>Covers primitive/temporal/nullable/collection mapping and emission edge cases.</summary>
public sealed class SchemaTypeMappingTests
{
    private static JsonElement Schema(string source, string toolName)
    {
        var json = GeneratorTestDriver.GetSchema(source, toolName);
        json.Should().NotBeNull();
        return JsonDocument.Parse(json!).RootElement.Clone();
    }

    [Fact]
    public void NumericAndTemporalTypes_MapToCorrectSchema()
    {
        const string source = """
        using System;
        using Native.Mcp;
        namespace Sample;
        public sealed record T
        {
            public double Ratio { get; init; }
            public decimal Amount { get; init; }
            public DateTime When { get; init; }
            public DateTimeOffset WhenOffset { get; init; }
            public Guid Id { get; init; }
            public int? OptionalCount { get; init; }
        }
        public sealed record O { public string R { get; init; } = ""; }
        public sealed partial class TemporalTool : IMcpTool<T, O>
        {
            public static string Name => "temporal";
            public static string Description => "d";
            public System.Threading.Tasks.Task<McpToolResult<O>> ExecuteAsync(
                T input, McpExecutionContext context, System.Threading.CancellationToken ct) =>
                System.Threading.Tasks.Task.FromResult(McpToolResult<O>.Success(new O()));
        }
        """;

        var props = Schema(source, "TemporalTool").GetProperty("properties");
        props.GetProperty("ratio").GetProperty("type").GetString().Should().Be("number");
        props.GetProperty("amount").GetProperty("type").GetString().Should().Be("number");
        props.GetProperty("when").GetProperty("format").GetString().Should().Be("date-time");
        props.GetProperty("whenOffset").GetProperty("format").GetString().Should().Be("date-time");
        props.GetProperty("id").GetProperty("format").GetString().Should().Be("uuid");
        props.GetProperty("optionalCount").GetProperty("type").GetString().Should().Be("integer");
    }

    [Fact]
    public void StringAndArrayLengthConstraints_Map()
    {
        const string source = """
        using System.Collections.Generic;
        using System.ComponentModel.DataAnnotations;
        using Native.Mcp;
        namespace Sample;
        public sealed record T
        {
            [MinLength(2)]
            [MaxLength(8)]
            public string Code { get; init; } = "";

            [MinLength(1)]
            public List<string> Tags { get; init; } = new();
        }
        public sealed record O { public string R { get; init; } = ""; }
        public sealed partial class LengthTool : IMcpTool<T, O>
        {
            public static string Name => "length";
            public static string Description => "d";
            public System.Threading.Tasks.Task<McpToolResult<O>> ExecuteAsync(
                T input, McpExecutionContext context, System.Threading.CancellationToken ct) =>
                System.Threading.Tasks.Task.FromResult(McpToolResult<O>.Success(new O()));
        }
        """;

        var props = Schema(source, "LengthTool").GetProperty("properties");
        props.GetProperty("code").GetProperty("minLength").GetInt32().Should().Be(2);
        props.GetProperty("code").GetProperty("maxLength").GetInt32().Should().Be(8);
        props.GetProperty("tags").GetProperty("type").GetString().Should().Be("array");
        props.GetProperty("tags").GetProperty("minItems").GetInt32().Should().Be(1);
    }

    [Fact]
    public void NonPartialTool_DoesNotEmitSchema()
    {
        const string source = """
        using Native.Mcp;
        namespace Sample;
        public sealed record T { public string A { get; init; } = ""; }
        public sealed record O { public string R { get; init; } = ""; }
        public sealed class NonPartialTool : IMcpTool<T, O>
        {
            public static string Name => "np";
            public static string Description => "d";
            public System.Threading.Tasks.Task<McpToolResult<O>> ExecuteAsync(
                T input, McpExecutionContext context, System.Threading.CancellationToken ct) =>
                System.Threading.Tasks.Task.FromResult(McpToolResult<O>.Success(new O()));
        }
        """;

        GeneratorTestDriver.GetSchema(source, "NonPartialTool").Should().BeNull();
    }

    [Fact]
    public void GlobalNamespaceTool_EmitsSchema()
    {
        const string source = """
        using Native.Mcp;
        public sealed record GT { public string A { get; init; } = ""; }
        public sealed record GO { public string R { get; init; } = ""; }
        public sealed partial class GlobalTool : IMcpTool<GT, GO>
        {
            public static string Name => "global";
            public static string Description => "d";
            public System.Threading.Tasks.Task<McpToolResult<GO>> ExecuteAsync(
                GT input, McpExecutionContext context, System.Threading.CancellationToken ct) =>
                System.Threading.Tasks.Task.FromResult(McpToolResult<GO>.Success(new GO()));
        }
        """;

        var schema = GeneratorTestDriver.GetSchema(source, "GlobalTool");
        schema.Should().NotBeNull();
        JsonDocument.Parse(schema!).RootElement.GetProperty("type").GetString().Should().Be("object");
    }

    [Fact]
    public void NonToolType_IsIgnored()
    {
        const string source = """
        namespace Sample;
        public interface IFoo { }
        public sealed class Bar : IFoo { }
        """;

        GeneratorTestDriver.GetSchema(source, "Bar").Should().BeNull();
    }
}
