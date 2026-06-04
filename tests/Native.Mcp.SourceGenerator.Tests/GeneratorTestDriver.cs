using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Native.Mcp.SourceGenerator;

namespace Native.Mcp.SourceGenerator.Tests;

/// <summary>Runs <see cref="InputSchemaGenerator"/> against a source snippet for snapshotting.</summary>
internal static class GeneratorTestDriver
{
    public static GeneratorDriverRunResult Run(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        // Build references from the runtime's trusted platform assemblies (corlib, System.*,
        // DataAnnotations, ...) and force-include Native.Mcp so IMcpTool resolves.
        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(System.IO.Path.PathSeparator)
            .Where(p => !string.IsNullOrEmpty(p));

        var paths = new HashSet<string>(trustedAssemblies, StringComparer.OrdinalIgnoreCase)
        {
            typeof(global::Native.Mcp.McpServerOptions).Assembly.Location,
        };

        var references = paths
            .Where(System.IO.File.Exists)
            .Select(p => MetadataReference.CreateFromFile(p))
            .Cast<MetadataReference>()
            .ToImmutableArray();

        var compilation = CSharpCompilation.Create(
            assemblyName: "GeneratorTests",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new InputSchemaGenerator());
        return driver.RunGenerators(compilation).GetRunResult();
    }

    /// <summary>Returns the generated <c>InputSchemaJson</c> for a tool, or null if not generated.</summary>
    public static string? GetSchema(string source, string toolName)
    {
        var result = Run(source);
        var generated = result.Results
            .SelectMany(r => r.GeneratedSources)
            .FirstOrDefault(s => s.HintName.EndsWith($"{toolName}.InputSchema.g.cs", StringComparison.Ordinal));

        if (generated.SourceText is null)
        {
            return null;
        }

        var text = generated.SourceText.ToString();
        const string marker = "\"\"\"";
        var start = text.IndexOf(marker, StringComparison.Ordinal);
        var end = text.IndexOf(marker, start + marker.Length, StringComparison.Ordinal);
        if (start < 0 || end < 0)
        {
            return null;
        }

        return text.Substring(start + marker.Length, end - start - marker.Length).Trim();
    }
}
