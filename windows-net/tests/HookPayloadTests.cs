using System.Text;
using System.Text.Json.Nodes;
using Coucou.Hook;

namespace Coucou.Tests;

[TestClass]
public sealed class HookPayloadTests
{
    static HookEvent? Read(string json, string argEvent = "", Dictionary<string, string>? env = null) =>
        HookPayload.Read(Encoding.UTF8.GetBytes(json), argEvent, name => env?.GetValueOrDefault(name), @"C:\here");

    [TestMethod]
    public void Decision_json_matches_the_documented_shape()
    {
        Assert.AreEqual(
            """{"hookSpecificOutput":{"hookEventName":"PermissionRequest","decision":{"behavior":"allow"}}}""",
            HookPayload.DecisionJson("allow"));
        Assert.AreEqual(
            """{"hookSpecificOutput":{"hookEventName":"PermissionRequest","decision":{"behavior":"deny","message":"Denied from Coucou"}}}""",
            HookPayload.DecisionJson("deny"));
        // "always" is an island concept; Claude Code just gets an allow.
        StringAssert.Contains(HookPayload.DecisionJson("always\n"), "\"behavior\":\"allow\"");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("maybe")]
    [DataRow("""{"permissionDecision":"allow"}""")]
    public void Anything_unrecognised_prints_nothing(string answer)
    {
        Assert.IsNull(HookPayload.DecisionJson(answer));
    }

    [TestMethod]
    public void Long_strings_are_cut_on_a_char_boundary()
    {
        var payload = Read(JsonSerializerString(new string('é', 4000)))!;
        var content = JsonNode.Parse(payload.Line)!["tool_input"]!["content"]!.GetValue<string>();
        Assert.IsLessThanOrEqualTo(HookPayload.MaxFieldLength + 3, Encoding.UTF8.GetByteCount(content));
        Assert.EndsWith("…", content);
        Assert.IsTrue(content.TrimEnd('…').All(c => c == 'é'), "a character was split");
    }

    [TestMethod]
    public void Surrogate_pairs_are_never_split()
    {
        var payload = Read(JsonSerializerString(string.Concat(Enumerable.Repeat("😀", 1000))))!;
        var content = JsonNode.Parse(payload.Line)!["tool_input"]!["content"]!.GetValue<string>();
        Assert.IsFalse(char.IsHighSurrogate(content[^2]), "a surrogate pair was split");
    }

    static string JsonSerializerString(string content) =>
        new JsonObject { ["hook_event_name"] = "PreToolUse", ["tool_input"] = new JsonObject { ["content"] = content } }.ToJsonString();

    [TestMethod]
    public void Big_fields_are_dropped_and_terminal_context_is_added()
    {
        var env = new Dictionary<string, string> { ["TERM_PROGRAM"] = "vscode", ["WT_SESSION"] = "abc" };
        var e = Read("""{"hook_event_name":"PostToolUse","tool_response":"huge","transcript_path":"C:/t.jsonl","tool_name":"Read"}""", env: env)!;
        var map = JsonNode.Parse(e.Line)!.AsObject();
        Assert.AreEqual("PostToolUse", e.Event);
        Assert.IsFalse(map.ContainsKey("tool_response"));
        Assert.IsFalse(map.ContainsKey("transcript_path"));
        Assert.AreEqual("Read", map.Str("tool_name"));
        Assert.AreEqual(@"C:\here", map.Str("cwd"), "a missing cwd is filled in");
        Assert.AreEqual("vscode", map.Str("term_program"));
        Assert.AreEqual("abc", map.Str("wt_session"));
        Assert.AreEqual("", map.Str("vscode_pid"), "unset variables still arrive, empty");
        Assert.EndsWith("\n", e.Line, "one line per event");
    }

    [TestMethod]
    public void The_event_name_falls_back_to_argv_and_a_given_cwd_is_kept()
    {
        var e = Read("""{"cwd":"D:\\project"}""", argEvent: "Stop")!;
        Assert.AreEqual("Stop", e.Event);
        var map = JsonNode.Parse(e.Line)!;
        Assert.AreEqual("Stop", map.Str("hook_event_name"));
        Assert.AreEqual(@"D:\project", map.Str("cwd"));
    }

    [TestMethod]
    public void The_host_window_is_looked_up_only_for_events_that_wait_on_the_person()
    {
        var lookups = 0;
        long? Host() { lookups++; return 4242; }

        foreach (var name in new[] { "PreToolUse", "PostToolUse", "SessionStart", "UserPromptSubmit", "SubagentStop" })
        {
            var e = HookPayload.Read(Encoding.UTF8.GetBytes($$"""{"hook_event_name":"{{name}}"}"""), "", _ => null, ".", Host)!;
            Assert.IsFalse(JsonNode.Parse(e.Line)!.AsObject().ContainsKey("host_window"), name);
        }
        Assert.AreEqual(0, lookups, "every tool call would pay for a process walk");

        foreach (var name in new[] { "PermissionRequest", "Notification", "Stop" })
        {
            var e = HookPayload.Read(Encoding.UTF8.GetBytes($$"""{"hook_event_name":"{{name}}"}"""), "", _ => null, ".", Host)!;
            Assert.AreEqual(4242L, JsonNode.Parse(e.Line)!["host_window"]!.GetValue<long>(), name);
        }

        // No window found: no field, rather than a zero the island would try to focus.
        var none = HookPayload.Read("""{"hook_event_name":"PermissionRequest"}"""u8, "", _ => null, ".", () => null)!;
        Assert.IsFalse(JsonNode.Parse(none.Line)!.AsObject().ContainsKey("host_window"));
    }

    [TestMethod]
    public void A_bom_is_accepted_and_garbage_is_ignored()
    {
        byte[] withBom = [0xEF, 0xBB, 0xBF, .. """{"hook_event_name":"Stop"}"""u8];
        Assert.AreEqual("Stop", HookPayload.Read(withBom, "", _ => null, ".")!.Event);
        Assert.IsNull(Read("not json"));
        Assert.IsNull(Read("[1,2]"));
        Assert.IsNull(HookPayload.Read([], "Stop", _ => null, "."));
    }
}
