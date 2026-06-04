# Native.Mcp.SourceGenerator

Roslyn incremental source generator for [`Native.Mcp`](https://github.com/swepay/native-mcp). It
runs at compile time and produces **zero runtime reflection**, so it is fully Native AOT
compatible.

It is bundled inside the `Native.Mcp` package — you normally don't reference it directly.

## What it generates

For every type implementing `Native.Mcp.IMcpTool<TInput, TOutput>`:

1. **`InputSchemaJson`** — a `public static string` on a `partial` of the tool, containing a JSON
   Schema (Draft 2020-12) derived from `TInput`. Surfaced in `tools/list`.
2. **`AddDiscoveredTools(this McpServerOptions, JsonSerializerContext)`** — an assembly-wide
   extension that registers every discovered tool using the supplied serializer context
   (AOT-safe).

The tool type must be `partial` and top-level for `InputSchemaJson` to be emitted.

## Schema mapping

| C# | JSON Schema |
|---|---|
| `string`, `char` | `"type": "string"` |
| `bool` | `"type": "boolean"` |
| integral types | `"type": "integer"` |
| `float`/`double`/`decimal` | `"type": "number"` |
| `DateTime`/`DateTimeOffset` | `"string"`, `"format": "date-time"` |
| `DateOnly` / `TimeOnly` | `"string"`, `format` `date` / `time` |
| `Guid` | `"string"`, `"format": "uuid"` |
| `enum` | `"type": "string"`, `"enum": [names]` |
| `T?` (nullable value) | underlying type |
| `T[]`, `IEnumerable<T>`, `List<T>`, ... | `"type": "array"`, `"items": <T>` |
| nested record/class | inline `"type": "object"` (cycle-guarded) |

| Attribute | Effect |
|---|---|
| `[Required]` or C# `required` | adds to `required[]` |
| `[Description("...")]` / `[Display(Description=...)]` | `description` |
| `[Range(min, max)]` | `minimum` / `maximum` |
| `[RegularExpression("...")]` | `pattern` |
| `[MinLength]`/`[MaxLength]`/`[StringLength]` | `minLength`/`maxLength` (or `minItems`/`maxItems` for arrays) |

Property names are emitted in camelCase to match the canonical Swepay JSON convention.

## License

MIT.
