using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Coucou.Tests;

[TestClass]
public sealed class SettingsTests
{
    [TestMethod]
    [DataRow(3.0, 3.0)]
    [DataRow(5.0, 5.0)]
    [DataRow(10.0, 10.0)]
    [DataRow(15.0, 10.0)] // the old default
    [DataRow(30.0, 10.0)]
    [DataRow(4.0, 3.0)]   // halfway: the shorter one
    [DataRow(1.0, 3.0)]
    public void Auto_close_lands_on_one_of_the_choices(double stored, double expected)
    {
        var json = $$"""{ "autoCloseInterval": {{stored}}, "language": "it" }""";
        var settings = JsonSerializer.Deserialize(json, AppJson.Default.Settings)!.Normalized();
        Assert.AreEqual(expected, settings.AutoCloseInterval);
        Assert.AreEqual("it", settings.Language);
    }

    [TestMethod]
    public void The_default_is_a_choice() =>
        CollectionAssert.Contains(Settings.AutoCloseChoices, new Settings().AutoCloseInterval);

    [TestMethod]
    public void The_pages_offer_the_same_choices()
    {
        var state = File.ReadAllText(Path.Combine(WebDir(), "src", "core", "state.ts"));
        var m = Regex.Match(state, @"AUTO_CLOSE_CHOICES = \[([^\]]*)\]");
        Assert.IsTrue(m.Success, "AUTO_CLOSE_CHOICES not found in state.ts");
        var pages = m.Groups[1].Value.Split(',').Select(s => double.Parse(s.Trim())).ToArray();
        CollectionAssert.AreEqual(Settings.AutoCloseChoices, pages);
    }

    static string WebDir([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "web"));
}
