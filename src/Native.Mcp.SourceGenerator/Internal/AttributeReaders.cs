using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Native.Mcp.SourceGenerator.Internal;

/// <summary>
/// Helpers for reading the data-annotation attributes that influence the generated JSON
/// Schema. Attributes are matched by simple class name so the generator does not depend on a
/// specific assembly/namespace for <c>System.ComponentModel.DataAnnotations</c>.
/// </summary>
internal static class AttributeReaders
{
    public static bool HasRequired(IPropertySymbol property) =>
        property.GetAttributes().Any(a => a.AttributeClass?.Name == "RequiredAttribute");

    public static string? GetDescription(IPropertySymbol property)
    {
        var attribute = property.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name is "DescriptionAttribute" or "DisplayAttribute");
        if (attribute is null)
        {
            return null;
        }

        if (attribute.AttributeClass?.Name == "DisplayAttribute")
        {
            return GetNamedString(attribute, "Description");
        }

        return attribute.ConstructorArguments.Length > 0
            ? attribute.ConstructorArguments[0].Value as string
            : null;
    }

    public static string? GetPattern(IPropertySymbol property)
    {
        var attribute = property.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "RegularExpressionAttribute");
        return attribute is { ConstructorArguments.Length: > 0 }
            ? attribute.ConstructorArguments[0].Value as string
            : null;
    }

    public static (string? Min, string? Max) GetRange(IPropertySymbol property)
    {
        var attribute = property.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "RangeAttribute");
        if (attribute is null || attribute.ConstructorArguments.Length < 2)
        {
            return (null, null);
        }

        // Range(min, max) — the (Type, string, string) overload is ignored (non-numeric bounds).
        var args = attribute.ConstructorArguments;
        if (args[0].Type?.SpecialType == SpecialType.System_Object || args[0].Value is string)
        {
            return (null, null);
        }

        return (FormatNumber(args[0].Value), FormatNumber(args[1].Value));
    }

    public static int? GetMinLength(IPropertySymbol property)
    {
        var attribute = property.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "MinLengthAttribute");
        if (attribute is { ConstructorArguments.Length: > 0 } && attribute.ConstructorArguments[0].Value is int min)
        {
            return min;
        }

        var stringLength = property.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "StringLengthAttribute");
        if (stringLength is not null)
        {
            var named = GetNamedInt(stringLength, "MinimumLength");
            if (named.HasValue)
            {
                return named;
            }
        }

        return null;
    }

    public static int? GetMaxLength(IPropertySymbol property)
    {
        var attribute = property.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "MaxLengthAttribute");
        if (attribute is { ConstructorArguments.Length: > 0 } && attribute.ConstructorArguments[0].Value is int max)
        {
            return max;
        }

        var stringLength = property.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "StringLengthAttribute");
        if (stringLength is { ConstructorArguments.Length: > 0 } && stringLength.ConstructorArguments[0].Value is int len)
        {
            return len;
        }

        return null;
    }

    private static string? GetNamedString(AttributeData attribute, string name) =>
        attribute.NamedArguments.FirstOrDefault(n => n.Key == name).Value.Value as string;

    private static int? GetNamedInt(AttributeData attribute, string name)
    {
        var match = attribute.NamedArguments.FirstOrDefault(n => n.Key == name);
        return match.Value.Value is int value ? value : null;
    }

    private static string? FormatNumber(object? value) => value switch
    {
        null => null,
        double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
        float f => f.ToString(System.Globalization.CultureInfo.InvariantCulture),
        decimal m => m.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => System.Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
    };
}
