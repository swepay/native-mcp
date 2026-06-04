using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Native.Mcp.SourceGenerator.Internal;

/// <summary>
/// Maps a C# input type to a JSON Schema (Draft 2020-12) document. Reads data-annotation
/// attributes, nullability and the C# <c>required</c> modifier. Nested objects and arrays of
/// objects are inlined recursively (with a cycle guard).
/// </summary>
internal static class TypeToJsonSchemaMapper
{
    private const string SchemaUri = "https://json-schema.org/draft/2020-12/schema";

    public static string Build(INamedTypeSymbol inputType)
    {
        var visited = new HashSet<string>(System.StringComparer.Ordinal);
        var root = BuildObjectSchema(inputType, isRoot: true, visited);
        return root.ToJsonString();
    }

    private static JObject BuildObjectSchema(INamedTypeSymbol type, bool isRoot, HashSet<string> visited)
    {
        var schema = new JObject();
        if (isRoot)
        {
            schema.Add("$schema", SchemaUri);
        }

        schema.Add("type", "object");

        var typeKey = type.ToDisplayString();
        if (!visited.Add(typeKey))
        {
            // Recursive type — stop descending.
            schema.Add("additionalProperties", JRaw.False);
            return schema;
        }

        var properties = new JObject();
        var required = new JArray();
        var hasRequired = false;

        foreach (var property in GetSchemaProperties(type))
        {
            var jsonName = ToCamelCase(property.Name);
            properties.Add(jsonName, BuildPropertySchema(property, visited));

            if (AttributeReaders.HasRequired(property) || property.IsRequired)
            {
                required.Add(new JString(jsonName));
                hasRequired = true;
            }
        }

        if (hasRequired)
        {
            schema.Add("required", required);
        }

        schema.Add("properties", properties);
        schema.Add("additionalProperties", JRaw.False);

        visited.Remove(typeKey);
        return schema;
    }

    private static JObject BuildPropertySchema(IPropertySymbol property, HashSet<string> visited)
    {
        var schema = BuildTypeSchema(property.Type, visited);

        var description = AttributeReaders.GetDescription(property);
        if (!string.IsNullOrEmpty(description))
        {
            schema.Add("description", description!);
        }

        var pattern = AttributeReaders.GetPattern(property);
        if (!string.IsNullOrEmpty(pattern))
        {
            schema.Add("pattern", pattern!);
        }

        var (min, max) = AttributeReaders.GetRange(property);
        if (min is not null)
        {
            schema.Add("minimum", new JRaw(min));
        }

        if (max is not null)
        {
            schema.Add("maximum", new JRaw(max));
        }

        var isArray = IsEnumerable(property.Type, out _);
        var minLength = AttributeReaders.GetMinLength(property);
        var maxLength = AttributeReaders.GetMaxLength(property);
        if (minLength is not null)
        {
            schema.Add(isArray ? "minItems" : "minLength", new JRaw(minLength.Value.ToString()));
        }

        if (maxLength is not null)
        {
            schema.Add(isArray ? "maxItems" : "maxLength", new JRaw(maxLength.Value.ToString()));
        }

        return schema;
    }

    private static JObject BuildTypeSchema(ITypeSymbol type, HashSet<string> visited)
    {
        var unwrapped = UnwrapNullable(type);

        if (unwrapped.TypeKind == TypeKind.Enum)
        {
            var schema = new JObject().Add("type", "string");
            var values = new JArray();
            foreach (var member in unwrapped.GetMembers())
            {
                if (member is IFieldSymbol { IsConst: true } field)
                {
                    values.Add(new JString(field.Name));
                }
            }

            schema.Add("enum", values);
            return schema;
        }

        var primitive = MapPrimitive(unwrapped);
        if (primitive is not null)
        {
            return primitive;
        }

        if (IsEnumerable(unwrapped, out var elementType) && elementType is not null)
        {
            return new JObject()
                .Add("type", "array")
                .Add("items", BuildTypeSchema(elementType, visited));
        }

        if (unwrapped is INamedTypeSymbol named && unwrapped.TypeKind is TypeKind.Class or TypeKind.Struct)
        {
            return BuildObjectSchema(named, isRoot: false, visited);
        }

        // Unknown/object — permissive.
        return new JObject().Add("type", "object");
    }

    private static JObject? MapPrimitive(ITypeSymbol type)
    {
        switch (type.SpecialType)
        {
            case SpecialType.System_String:
            case SpecialType.System_Char:
                return new JObject().Add("type", "string");
            case SpecialType.System_Boolean:
                return new JObject().Add("type", "boolean");
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
                return new JObject().Add("type", "integer");
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Decimal:
                return new JObject().Add("type", "number");
            case SpecialType.System_DateTime:
                return new JObject().Add("type", "string").Add("format", "date-time");
        }

        return type.Name switch
        {
            "DateTimeOffset" => new JObject().Add("type", "string").Add("format", "date-time"),
            "DateOnly" => new JObject().Add("type", "string").Add("format", "date"),
            "TimeOnly" => new JObject().Add("type", "string").Add("format", "time"),
            "Guid" => new JObject().Add("type", "string").Add("format", "uuid"),
            "Uri" => new JObject().Add("type", "string").Add("format", "uri"),
            _ => null,
        };
    }

    private static IEnumerable<IPropertySymbol> GetSchemaProperties(INamedTypeSymbol type)
    {
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        for (var current = type; current is not null && current.SpecialType != SpecialType.System_Object;
             current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                if (member is IPropertySymbol
                    {
                        DeclaredAccessibility: Accessibility.Public,
                        IsStatic: false,
                        IsIndexer: false,
                        GetMethod: not null,
                    } property
                    && property.Name != "EqualityContract"
                    && seen.Add(property.Name))
                {
                    yield return property;
                }
            }
        }
    }

    private static ITypeSymbol UnwrapNullable(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } named)
        {
            return named.TypeArguments[0];
        }

        return type;
    }

    private static bool IsEnumerable(ITypeSymbol type, out ITypeSymbol? elementType)
    {
        elementType = null;
        if (type.SpecialType == SpecialType.System_String)
        {
            return false;
        }

        if (type is IArrayTypeSymbol array)
        {
            elementType = array.ElementType;
            return true;
        }

        if (type is INamedTypeSymbol { IsGenericType: true } named)
        {
            if (named.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
            {
                elementType = named.TypeArguments[0];
                return true;
            }

            foreach (var iface in named.AllInterfaces)
            {
                if (iface.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
                {
                    elementType = iface.TypeArguments[0];
                    return true;
                }
            }
        }

        return false;
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
        {
            return name;
        }

        var chars = name.ToCharArray();
        chars[0] = char.ToLowerInvariant(chars[0]);
        return new string(chars);
    }
}
