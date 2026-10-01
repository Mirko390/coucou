using System.Text;
using System.Text.Json.Nodes;
using Coucou.Hooks;

namespace Coucou.Tests;

[TestClass]
public sealed class ClaudeHooksTests
{
    const string Where = "settings.json";
    const string HookExe = @"C:\Users\someone\AppData\Local\Coucou\bin\coucou-hook.exe";

    static ClaudeHooks Hooks(string path = Where) => new(path, HookExe);

    static byte[] Bom(string json) => [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(json)];

    [TestMethod]
    public void A_utf8_bom_is_stripped_not_treated_as_corruption()
    {
        // PowerShell 5's `Set-Content -Encoding utf8` produces exactly this.
        var parsed = ClaudeHooks.ParseSettings(Bom("""{"model":"opus","hooks":{}}"""), Where);
        Assert.AreEqual("opus", parsed.Str("model"));
    }

    [TestMethod]
    [DataRow("{ not json")]
    [DataRow("[1,2,3]")]
    [DataRow("\"a string\"")]
    [DataRow("""{"model":"a","model":"b"}""")]
    public void Unreadable_content_is_an_error_never_an_empty_object(string content)
    {
        // This is the whole bug the Tauri build once had: returning {} here meant
        // the merge produced a file containing nothing but Coucou's hooks, and the
        // write replaced everything the user had.
        Assert.ThrowsExactly<UserFacingException>(() => ClaudeHooks.ParseSettings(Encoding.UTF8.GetBytes(content), Where));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("  \n\t ")]
    public void Empty_and_whitespace_files_start_from_nothing(string content)
    {
        Assert.AreEqual(0, ClaudeHooks.ParseSettings(Encoding.UTF8.GetBytes(content), Where).Count);
    }

    [TestMethod]
    public void Merging_keeps_every_other_setting_and_every_foreign_hook()
    {
        var existing = JsonNode.Parse("""
            {
              "model": "claude-opus-5",
              "theme": "dark",
              "enabledPlugins": ["a", "b"],
              "hooks": {
                "PreToolUse": [
                  { "hooks": [{ "type": "command", "command": "someone-elses-tool.exe" }] }
                ],
                "SomeEventWeDoNotTouch": [
                  { "hooks": [{ "type": "command", "command": "keep-me.exe" }] }
                ]
              }
            }
            """)!.AsObject();

        var after = Hooks().Merged(existing);
        Assert.AreEqual("claude-opus-5", after.Str("model"));
        Assert.AreEqual("dark", after.Str("theme"));
        Assert.IsTrue(JsonNode.DeepEquals(existing["enabledPlugins"], after["enabledPlugins"]));

        var pre = after["hooks"]!["PreToolUse"]!.AsArray();
        Assert.IsTrue(pre.Any(e => e!.ToJsonString().Contains("someone-elses-tool.exe")), "another tool's hook was dropped");
        Assert.IsTrue(pre.Any(ClaudeHooks.EntryIsOurs), "our own hook was not added");
        Assert.IsInstanceOfType<JsonArray>(after["hooks"]!["SomeEventWeDoNotTouch"]);

        // Every event gets exactly one Coucou entry, and the command is the quoted
        // relay path in forward slashes — Claude Code runs it through Git Bash.
        foreach (var (hookEvent, timeout) in ClaudeHooks.HookEvents)
        {
            var ours = after["hooks"]![hookEvent]!.AsArray().Where(ClaudeHooks.EntryIsOurs).ToList();
            Assert.HasCount(1, ours);
            var hook = ours[0]!["hooks"]![0]!;
            Assert.AreEqual($"\"{HookExe.Replace('\\', '/')}\" {hookEvent}", hook.Str("command"));
            Assert.AreEqual(timeout, hook["timeout"]!.GetValue<int>());
        }

        // And removing ours puts it back exactly as it was.
        Assert.IsTrue(JsonNode.DeepEquals(existing, ClaudeHooks.WithoutOurs(after)));
    }

    [TestMethod]
    public void Merging_twice_does_not_add_a_second_entry()
    {
        var once = Hooks().Merged([]);
        var twice = Hooks().Merged(once);
        Assert.IsTrue(JsonNode.DeepEquals(once, twice));
    }

    [TestMethod]
    public void A_fingerprint_notices_any_change()
    {
        Assert.AreEqual(ClaudeHooks.Fingerprint("{}"u8), ClaudeHooks.Fingerprint("{}"u8));
        Assert.AreNotEqual(ClaudeHooks.Fingerprint("{}"u8), ClaudeHooks.Fingerprint("{ }"u8));
        Assert.AreNotEqual(ClaudeHooks.Fingerprint(""u8), ClaudeHooks.Fingerprint("{}"u8));
        // The Tauri build's FNV-1a, bit for bit.
        Assert.AreEqual("cbf29ce484222325", ClaudeHooks.Fingerprint(""u8));
    }

    [TestMethod]
    public void Pretty_printing_leaves_other_peoples_strings_and_numbers_alone()
    {
        // JsonSerializer would write `&&` as \u0026\u0026 and é as \u00E9: the same
        // JSON, but somebody else's file rewritten and a noisy diff.
        var json = """{"hooks":{"Stop":[{"hooks":[{"type":"command","command":"say \"done\" && echo é > log <x>"}]}]},"n":1.50,"big":12345678901234567890}""";
        var pretty = JsonText.Pretty(JsonNode.Parse(json));
        StringAssert.Contains(pretty, "say \\\"done\\\" && echo é > log <x>");
        StringAssert.Contains(pretty, "\"n\": 1.50");
        StringAssert.Contains(pretty, "\"big\": 12345678901234567890");
        Assert.IsTrue(JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(pretty)));
    }

    [TestMethod]
    public void Pretty_printing_matches_serde_json_layout()
    {
        var pretty = JsonText.Pretty(JsonNode.Parse("""{"a":[],"b":{},"c":[1,{"d":null}],"e":"tab\there"}"""));
        Assert.AreEqual(
            "{\n  \"a\": [],\n  \"b\": {},\n  \"c\": [\n    1,\n    {\n      \"d\": null\n    }\n  ],\n  \"e\": \"tab\\there\"\n}",
            pretty);
    }

    [TestMethod]
    public void The_diff_shows_changes_with_context_and_says_when_there_are_none()
    {
        Assert.AreEqual("No change.", ClaudeHooks.UnifiedDiff("a\nb", "a\nb"));
        var diff = ClaudeHooks.UnifiedDiff("1\n2\n3\n4\n5\n6\n7\n8\n9", "1\n2\n3\n4\n5\n6\n7\nX\n9");
        Assert.AreEqual("  …\n  5\n  6\n  7\n- 8\n+ X\n  9\n", diff);
    }

    /// <summary>Everything filesystem-shaped, in a throwaway directory.</summary>
    [TestMethod]
    public void Writing_backs_up_preserves_and_refuses_a_changed_file()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"coucou-hooks-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(tmp, ".claude"));
        var path = Path.Combine(tmp, ".claude", "settings.json");
        var hooks = Hooks(path);
        try
        {
            // A real-shaped file, written the way PowerShell 5 would: UTF-8 with BOM.
            var bytes = Bom("""{"model":"claude-opus-5","theme":"dark","tui":{"x":1},"hooks":{"PreToolUse":[{"hooks":[{"type":"command","command":"other-tool.exe"}]}]}}""");
            File.WriteAllBytes(path, bytes);

            // Install.
            var plan = hooks.Preview(install: true);
            StringAssert.Contains(plan.Diff, "coucou-hook", "the diff must show what changes");
            var backup = hooks.Write(install: true, plan.Fingerprint);

            // The backup holds the original bytes, BOM and all.
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(backup));

            // Everything else survived, and so did the other tool's hook.
            var after = JsonNode.Parse(File.ReadAllText(path))!;
            Assert.AreEqual("claude-opus-5", after.Str("model"));
            Assert.AreEqual("dark", after.Str("theme"));
            Assert.AreEqual(1, after["tui"]!["x"]!.GetValue<int>());
            Assert.IsTrue(after["hooks"]!["PreToolUse"]!.AsArray().Any(e => e!.ToJsonString().Contains("other-tool.exe")));
            Assert.IsTrue(hooks.Status().Installed);
            Assert.IsFalse(File.ReadAllBytes(path).AsSpan().StartsWith("\uFEFF"u8), "written back without a BOM");

            // A file that moved since the preview is refused, and left alone.
            var stale = hooks.Preview(install: false);
            File.WriteAllText(path, """{"model":"someone-else-edited-this"}""");
            var refused = Assert.ThrowsExactly<UserFacingException>(() => hooks.Write(install: false, stale.Fingerprint));
            StringAssert.Contains(refused.Message, "changed since the preview");
            Assert.AreEqual("someone-else-edited-this", JsonNode.Parse(File.ReadAllText(path)).Str("model"));

            // Uninstalling removes ours and nothing else.
            File.WriteAllText(path, File.ReadAllText(backup).TrimStart('\uFEFF'));
            hooks.Write(true, hooks.Preview(true).Fingerprint);
            hooks.Write(false, hooks.Preview(false).Fingerprint);
            Assert.IsFalse(hooks.Status().Installed);
            Assert.IsTrue(JsonNode.DeepEquals(
                JsonNode.Parse(Encoding.UTF8.GetString(bytes.AsSpan(3))),
                JsonNode.Parse(File.ReadAllText(path))));

            // Content we cannot parse is refused before anything is written.
            File.WriteAllText(path, "{ broken");
            Assert.ThrowsExactly<UserFacingException>(() => hooks.Preview(install: true));
            Assert.ThrowsExactly<UserFacingException>(() => hooks.Write(install: true, "whatever"));
            Assert.AreEqual("{ broken", File.ReadAllText(path));

            // No leftover temp files next to settings.json.
            Assert.IsEmpty(Directory.GetFiles(Path.Combine(tmp, ".claude"), "*.coucou-*"));
        }
        finally
        {
            Directory.Delete(tmp, recursive: true);
        }
    }

    [TestMethod]
    public void A_missing_settings_file_is_created_with_only_our_hooks()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"coucou-hooks-{Guid.NewGuid():N}");
        var path = Path.Combine(tmp, ".claude", "settings.json");
        try
        {
            var hooks = Hooks(path);
            Assert.IsFalse(hooks.Status().Installed);
            hooks.Write(install: true, hooks.Preview(install: true).Fingerprint);
            var written = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            Assert.HasCount(1, written);
            Assert.IsTrue(hooks.Status().Installed);
        }
        finally
        {
            if (Directory.Exists(tmp)) Directory.Delete(tmp, recursive: true);
        }
    }
}
