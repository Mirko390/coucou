// Forgiving reads over JSON whose shape we do not control (API responses, hook
// payloads, page messages): a missing key, a null or a value of the wrong type
// all read as null, the way serde_json's `get(..).and_then(as_str)` did.

using System.Text.Json.Nodes;

namespace Coucou;

static class JsonAccess
{
    public static JsonNode? Get(this JsonNode? node, string key) =>
        node is JsonObject obj && obj.TryGetPropertyValue(key, out var value) ? value : null;

    public static string? Str(this JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var s) ? s : null;

    public static string? Str(this JsonNode? node, string key) => node.Get(key).Str();

    public static long? Int(this JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<long>(out var n) ? n : null;

    public static double? Num(this JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<double>(out var n) ? n : null;

    public static bool? Bool(this JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<bool>(out var b) ? b : null;

    public static JsonArray? Arr(this JsonNode? node) => node as JsonArray;

    public static JsonObject? Obj(this JsonNode? node) => node as JsonObject;
}
