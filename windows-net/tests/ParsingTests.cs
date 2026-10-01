using System.Text.Json.Nodes;
using Coucou.Integrations;

namespace Coucou.Tests;

[TestClass]
public sealed class ParsingTests
{
    [TestMethod]
    public void Files_become_the_right_kind_of_block()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"coucou-blocks-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmp);
        try
        {
            var text = Path.Combine(tmp, "code.cs");
            File.WriteAllText(text, "class A {}");
            Assert.AreEqual("File contents:\nclass A {}", ClaudeChat.FileBlock(text).Str("text"));

            var png = Path.Combine(tmp, "pic.PNG");
            File.WriteAllBytes(png, [1, 2, 3]);
            var image = ClaudeChat.FileBlock(png)!;
            Assert.AreEqual("image", image.Str("type"));
            Assert.AreEqual("image/png", image.Get("source").Str("media_type"));
            Assert.AreEqual("AQID", image.Get("source").Str("data"));

            var pdf = Path.Combine(tmp, "doc.pdf");
            File.WriteAllBytes(pdf, [0x25, 0x50]);
            Assert.AreEqual("document", ClaudeChat.FileBlock(pdf).Str("type"));

            // Binary files are skipped, not sent as mojibake; so are huge text files.
            var binary = Path.Combine(tmp, "blob.bin");
            File.WriteAllBytes(binary, [0xFF, 0xFE, 0x00, 0xC3]);
            Assert.IsNull(ClaudeChat.FileBlock(binary));
            var huge = Path.Combine(tmp, "huge.txt");
            File.WriteAllText(huge, new string('x', 200_001));
            Assert.IsNull(ClaudeChat.FileBlock(huge));
            Assert.IsNull(ClaudeChat.FileBlock(Path.Combine(tmp, "missing.txt")));
        }
        finally
        {
            Directory.Delete(tmp, recursive: true);
        }
    }

    [TestMethod]
    public void Chat_context_comes_from_the_page_shape()
    {
        Assert.AreEqual(
            new ChatContext.File("a.pdf", @"C:\a.pdf"),
            ChatContext.From(JsonNode.Parse("""{"kind":"file","name":"a.pdf","path":"C:\\a.pdf"}""")));
        Assert.AreEqual(
            new ChatContext.Window("Code", "main.rs", null),
            ChatContext.From(JsonNode.Parse("""{"kind":"window","appName":"Code","title":"main.rs"}""")));
        Assert.IsNull(ChatContext.From(null));
        Assert.IsNull(ChatContext.From(JsonNode.Parse("[]")));
    }

    [TestMethod]
    public void Notion_pages_and_databases_get_a_title()
    {
        var page = Pollers.ParseNotionPage(JsonNode.Parse("""
            { "id": "p1", "object": "page", "last_edited_time": "2026-09-30T10:00:00Z", "url": "https://notion.so/p1",
              "icon": { "type": "emoji", "emoji": "📝" },
              "properties": { "Tags": { "type": "multi_select" }, "Name": { "type": "title", "title": [{ "plain_text": "Roadmap" }] } } }
            """))!;
        Assert.AreEqual("Roadmap", page.Str("title"));
        Assert.AreEqual("📝", page.Str("emoji"));

        var db = Pollers.ParseNotionPage(JsonNode.Parse("""
            { "id": "d1", "object": "database", "last_edited_time": "x", "title": [{ "plain_text": "" }] }
            """))!;
        Assert.AreEqual("Untitled", db.Str("title"));
        Assert.AreEqual("https://notion.so", db.Str("url"));
        Assert.IsNull(db.Str("emoji"));

        Assert.IsNull(Pollers.ParseNotionPage(JsonNode.Parse("""{ "id": "no-date" }""")));
    }

    [TestMethod]
    public void N8n_details_say_what_ran_or_what_broke()
    {
        var ok = JsonNode.Parse("""
            { "data": { "resultData": { "lastNodeExecuted": "Send", "runData": { "Send": [ { "data": { "main": [ [
              { "json": { "to": "a@b.c", "count": 3, "tags": [1, 2], "meta": {}, "extra": true } }, { "json": {} } ] ] } } ] } } } }
            """)!;
        Assert.AreEqual("→ Send · 2 items\nto: a@b.c\ncount: 3\ntags: [2]\nmeta: {…}", Pollers.N8nDetail(ok, success: true));

        var failed = JsonNode.Parse("""
            { "data": { "resultData": { "error": { "message": "boom", "node": { "name": "HTTP" } } } } }
            """)!;
        Assert.AreEqual("HTTP\nboom", Pollers.N8nDetail(failed, success: false));

        var failedInRun = JsonNode.Parse("""
            { "data": { "resultData": { "runData": { "Fetch": [ { "error": { "message": "timeout" } } ] } } } }
            """)!;
        Assert.AreEqual("Fetch\ntimeout", Pollers.N8nDetail(failedInRun, success: false));

        Assert.IsNull(Pollers.N8nDetail(JsonNode.Parse("{}")!, success: true));
    }
}
