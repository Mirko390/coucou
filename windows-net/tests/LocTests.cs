using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Coucou.Tests;

[TestClass]
public sealed partial class LocTests
{
    static string AppDir([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "app"));

    [GeneratedRegex("""Loc\.T\(\s*"((?:[^"\\]|\\.)*)"(?=\s*[,)])""")]
    private static partial Regex LocCall();

    [TestMethod]
    public void Every_host_string_has_an_italian_entry_and_none_is_left_over()
    {
        var used = new HashSet<string>();
        foreach (var file in Directory.EnumerateFiles(AppDir(), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;
            foreach (Match m in LocCall().Matches(File.ReadAllText(file)))
                used.Add(Regex.Unescape(m.Groups[1].Value));
        }
        // Reach Loc.T through a variable: Pollers.StatusError's forbidden hints.
        used.UnionWith(
        [
            "Use a secret key (sk_live_… not pk_live_…)", "Token lacks the needed scope",
            "Token lacks access", "Key lacks access", "Integration lacks access",
        ]);
        // The tray items, registered through Tray.Add.
        used.UnionWith(["Open Coucou", "Settings…", "Pause", "Quit"]);

        var keys = Loc.Keys.ToHashSet();
        CollectionAssert.AreEquivalent(used.Order().ToList(), keys.Order().ToList());
    }

    [TestMethod]
    public void Translations_keep_their_placeholders()
    {
        foreach (var key in Loc.Keys)
        {
            var english = Regex.Matches(key, @"\{\d+\}").Select(m => m.Value).Order();
            var italian = Regex.Matches(ItalianFor(key), @"\{\d+\}").Select(m => m.Value).Order();
            CollectionAssert.AreEqual(english.ToList(), italian.ToList(), key);
        }
    }

    [TestMethod]
    public void Untouched_the_host_speaks_english_and_explicit_choices_win()
    {
        Assert.AreEqual("en", Loc.Language, "only the app picks a language");
        Assert.AreEqual("No change.", Loc.T("No change."));
        Assert.AreEqual("it", Loc.Resolve("it"));
        Assert.AreEqual("en", Loc.Resolve("en"));
        Assert.AreEqual("unknown text", Loc.T("unknown text"));
    }

    /// <summary>The Italian entry, read without switching the process-wide language.</summary>
    static string ItalianFor(string key)
    {
        var table = (Dictionary<string, string>)typeof(Loc)
            .GetField("It", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .GetValue(null)!;
        return table[key];
    }
}
