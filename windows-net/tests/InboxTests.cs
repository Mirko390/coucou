namespace Coucou.Tests;

[TestClass]
public sealed class InboxTests
{
    [TestMethod]
    public void Ingest_copies_and_never_overwrites()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"coucou-inbox-{Guid.NewGuid():N}");
        var inbox = Path.Combine(tmp, "inbox");
        Directory.CreateDirectory(tmp);
        try
        {
            var source = Path.Combine(tmp, "note.txt");
            File.WriteAllText(source, "hello");

            var first = Inbox.Ingest(source, inbox);
            Assert.AreEqual("note.txt", first.Name);
            Assert.AreEqual(5, first.Size);
            Assert.AreEqual("hello", File.ReadAllText(first.Path));

            // A second drop of the same name must not clobber the first copy.
            File.WriteAllText(source, "second");
            var second = Inbox.Ingest(source, inbox);
            Assert.AreNotEqual(first.Path, second.Path);
            Assert.AreEqual("note (2).txt", Path.GetFileName(second.Path));
            Assert.AreEqual("hello", File.ReadAllText(first.Path));
            Assert.AreEqual("second", File.ReadAllText(second.Path));

            // Folders are refused rather than silently ignored.
            Assert.ThrowsExactly<UserFacingException>(() => Inbox.Ingest(tmp, inbox));
            Assert.ThrowsExactly<UserFacingException>(() => Inbox.Ingest(Path.Combine(tmp, "missing.txt"), inbox));

            // An ancient source must not arrive already older than the sweep window.
            var old = Path.Combine(tmp, "ancient.txt");
            File.WriteAllText(old, "old");
            File.SetLastWriteTimeUtc(old, DateTime.UtcNow - TimeSpan.FromDays(8));
            var aged = Inbox.Ingest(old, inbox);
            Assert.IsTrue(File.Exists(aged.Path), "a file copied just now was swept as if it were a week old");

            // Whereas a copy that really has been sitting there for a week goes.
            File.SetLastWriteTimeUtc(first.Path, DateTime.UtcNow - TimeSpan.FromDays(8));
            Inbox.Ingest(source, inbox);
            Assert.IsFalse(File.Exists(first.Path));
        }
        finally
        {
            Directory.Delete(tmp, recursive: true);
        }
    }
}
