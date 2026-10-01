// Pretty-printing for Claude Code's settings.json.
//
// Not JsonSerializer: its writer escapes every non-ASCII character and the HTML
// ones (`&`, `<`, `>`, `'`, `+`), so a hook command like `a && b` would come back
// as `a && b` — the same JSON, but somebody else's file rewritten and a
// diff full of lines that did not really change. This writes what serde_json's
// pretty printer wrote in the Tauri build: two-space indent, `"key": value`, and
// only `"`, `\` and control characters escaped. Numbers keep their original text.

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Coucou.Hooks;

static class JsonText
{
    public static string Pretty(JsonNode? node)
    {
        var sb = new StringBuilder();
        Write(sb, node, 0);
        return sb.ToString();
    }

    static void Write(StringBuilder sb, JsonNode? node, int depth)
    {
        switch (node)
        {
            case null:
                sb.Append("null");
                break;
            case JsonObject obj when obj.Count == 0:
                sb.Append("{}");
                break;
            case JsonObject obj:
                sb.Append("{\n");
                var first = true;
                foreach (var (key, value) in obj)
                {
                    if (!first) sb.Append(",\n");
                    first = false;
                    Indent(sb, depth + 1);
                    WriteString(sb, key);
                    sb.Append(": ");
                    Write(sb, value, depth + 1);
                }
                sb.Append('\n');
                Indent(sb, depth);
                sb.Append('}');
                break;
            case JsonArray array when array.Count == 0:
                sb.Append("[]");
                break;
            case JsonArray array:
                sb.Append("[\n");
                for (var i = 0; i < array.Count; i++)
                {
                    if (i > 0) sb.Append(",\n");
                    Indent(sb, depth + 1);
                    Write(sb, array[i], depth + 1);
                }
                sb.Append('\n');
                Indent(sb, depth);
                sb.Append(']');
                break;
            case JsonValue value when value.GetValueKind() == JsonValueKind.String:
                WriteString(sb, value.GetValue<string>());
                break;
            case JsonValue value:
                // Numbers, true, false: parsed ones keep their exact source text.
                sb.Append(value.ToJsonString());
                break;
        }
    }

    static void Indent(StringBuilder sb, int depth) => sb.Append(' ', depth * 2);

    static void WriteString(StringBuilder sb, string s)
    {
        sb.Append('"');
        foreach (var c in s)
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
                case < ' ':
                    sb.Append("\\u00").Append(((int)c).ToString("x2", CultureInfo.InvariantCulture));
                    break;
                default: sb.Append(c); break;
            }
        }
        sb.Append('"');
    }
}
