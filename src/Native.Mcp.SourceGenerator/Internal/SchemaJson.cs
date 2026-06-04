using System.Collections.Generic;
using System.Text;

namespace Native.Mcp.SourceGenerator.Internal;

/// <summary>A tiny, ordered JSON model used to build deterministic schema output.</summary>
internal abstract class JNode
{
    public abstract void Write(StringBuilder sb, int indent);

    protected static void Indent(StringBuilder sb, int indent) => sb.Append(' ', indent * 2);

    public string ToJsonString()
    {
        var sb = new StringBuilder();
        Write(sb, 0);
        return sb.ToString();
    }
}

/// <summary>An ordered JSON object.</summary>
internal sealed class JObject : JNode
{
    private readonly List<KeyValuePair<string, JNode>> _members = new();

    public JObject Add(string key, JNode value)
    {
        _members.Add(new KeyValuePair<string, JNode>(key, value));
        return this;
    }

    public JObject Add(string key, string value) => Add(key, new JString(value));

    public bool HasMembers => _members.Count > 0;

    public override void Write(StringBuilder sb, int indent)
    {
        if (_members.Count == 0)
        {
            sb.Append("{}");
            return;
        }

        sb.Append("{\n");
        for (var i = 0; i < _members.Count; i++)
        {
            Indent(sb, indent + 1);
            sb.Append('"').Append(JString.Escape(_members[i].Key)).Append("\": ");
            _members[i].Value.Write(sb, indent + 1);
            if (i < _members.Count - 1)
            {
                sb.Append(',');
            }

            sb.Append('\n');
        }

        Indent(sb, indent);
        sb.Append('}');
    }
}

/// <summary>A JSON array.</summary>
internal sealed class JArray : JNode
{
    private readonly List<JNode> _items = new();

    public JArray Add(JNode item)
    {
        _items.Add(item);
        return this;
    }

    public override void Write(StringBuilder sb, int indent)
    {
        if (_items.Count == 0)
        {
            sb.Append("[]");
            return;
        }

        sb.Append("[\n");
        for (var i = 0; i < _items.Count; i++)
        {
            Indent(sb, indent + 1);
            _items[i].Write(sb, indent + 1);
            if (i < _items.Count - 1)
            {
                sb.Append(',');
            }

            sb.Append('\n');
        }

        Indent(sb, indent);
        sb.Append(']');
    }
}

/// <summary>A JSON string.</summary>
internal sealed class JString : JNode
{
    private readonly string _value;

    public JString(string value) => _value = value;

    public override void Write(StringBuilder sb, int indent) =>
        sb.Append('"').Append(Escape(_value)).Append('"');

    public static string Escape(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20)
                    {
                        sb.Append("\\u").Append(((int)c).ToString("x4"));
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }

        return sb.ToString();
    }
}

/// <summary>A raw JSON literal (number, boolean) emitted verbatim.</summary>
internal sealed class JRaw : JNode
{
    private readonly string _literal;

    public JRaw(string literal) => _literal = literal;

    public static readonly JRaw True = new("true");
    public static readonly JRaw False = new("false");

    public override void Write(StringBuilder sb, int indent) => sb.Append(_literal);
}
