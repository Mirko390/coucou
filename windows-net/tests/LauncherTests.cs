namespace Coucou.Tests;

[TestClass]
public sealed class LauncherTests
{
    [TestMethod]
    public void Visual_studio_gets_the_folders_solution_when_there_is_exactly_one()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"coucou-sln-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmp);
        try
        {
            Assert.IsNull(Launcher.SolutionIn(tmp), "no solution: open the folder");

            File.WriteAllText(Path.Combine(tmp, "App.sln"), "");
            Assert.AreEqual(Path.Combine(tmp, "App.sln"), Launcher.SolutionIn(tmp));

            // An .slnx beside the .sln it was migrated from wins.
            File.WriteAllText(Path.Combine(tmp, "App.slnx"), "");
            Assert.AreEqual(Path.Combine(tmp, "App.slnx"), Launcher.SolutionIn(tmp));

            // Two of a kind: no way to tell which one is meant, so the folder it is.
            File.WriteAllText(Path.Combine(tmp, "Other.slnx"), "");
            Assert.IsNull(Launcher.SolutionIn(tmp));

            Assert.IsNull(Launcher.SolutionIn(Path.Combine(tmp, "missing")));
        }
        finally
        {
            Directory.Delete(tmp, recursive: true);
        }
    }
}
