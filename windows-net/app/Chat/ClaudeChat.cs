// Claude API client — the same integration as ClaudeService.swift: multi-turn
// chat with web search, and files sent as document/image/text blocks.
//
// Everything happens here rather than in the island: the API key never leaves
// the Credential Manager, and file bytes never cross to the page.
//
// Plain HTTP, like the macOS app, rather than the Anthropic SDK: the app takes no
// dependency it can do without (CLAUDE.md).

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Coucou;

/// <summary>What the first message of a chat is about.</summary>
abstract record ChatContext
{
    public sealed record File(string Name, string Path) : ChatContext;

    public sealed record Window(string AppName, string Title, string? Url) : ChatContext;

    /// <summary>From the page's <c>{ kind: "file" | "window", … }</c>, or null.</summary>
    public static ChatContext? From(JsonNode? node) => node.Str("kind") switch
    {
        "file" => new File(node.Str("name") ?? "", node.Str("path") ?? ""),
        "window" => new Window(node.Str("appName") ?? "", node.Str("title") ?? "", node.Str("url")),
        _ => null,
    };
}

sealed class ClaudeChat
{
    const string Endpoint = "https://api.anthropic.com/v1/messages";
    const string AnthropicVersion = "2023-06-01";

    /// <summary>
    /// Server-side fallback: on a policy decline the API retries the same request
    /// on a fallback model inside the same call, so the island never shows a dead end.
    /// </summary>
    const string FallbackBeta = "server-side-fallback-2026-07-01";

    const int MaxTokens = 4096;

    /// <summary>Text and code files are inlined; anything larger is skipped, as on macOS.</summary>
    const long MaxInlineText = 200_000;

    public const string DefaultModel = "claude-opus-5";

    const string SystemPrompt =
        "You are Mochi, a personal AI assistant living at the top of the user's screen. " +
        "You have web search access and can help with absolutely anything — research, coding, finding places, recommendations, tasks, questions. " +
        "Respond in the user's language. Be thorough and complete — use as much detail as the task requires. " +
        "No markdown formatting (no **, no ##, no bullet dashes). Use plain text with line breaks.";

    readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(90) };

    /// <summary>Full multi-turn history, including tool_use / tool_result blocks.</summary>
    readonly List<JsonNode> messages = [];

    readonly Lock gate = new();

    public void Reset()
    {
        lock (gate) messages.Clear();
    }

    /// <summary>One chat turn. Returns the assistant's text.</summary>
    /// <exception cref="UserFacingException">With the message the island shows in the note view.</exception>
    public async Task<string> Send(string model, string query, ChatContext? context)
    {
        var key = Secrets.Get("anthropic-api-key")
            ?? throw new UserFacingException(Loc.T("API key missing. Open settings."));

        var content = new JsonArray();
        bool first;
        lock (gate) first = messages.Count == 0;

        // File / window context rides along with the first message only, exactly
        // like ClaudeService.chat().
        if (first)
        {
            switch (context)
            {
                case ChatContext.File file:
                    if (FileBlock(file.Path) is { } block) content.Add(block);
                    content.Add(Text($"File: {file.Name}"));
                    break;
                case ChatContext.Window window:
                    var text = $"Context — App: {window.AppName}, Window: {window.Title}";
                    if (window.Url is not null) text += $", URL: {window.Url}";
                    content.Add(Text(text));
                    break;
            }
        }
        content.Add(Text(query));

        JsonArray history;
        lock (gate)
        {
            messages.Add(new JsonObject { ["role"] = "user", ["content"] = content });
            history = new JsonArray(messages.Select(m => m.DeepClone()).ToArray());
        }

        var body = new JsonObject
        {
            ["model"] = model,
            ["max_tokens"] = MaxTokens,
            ["system"] = SystemPrompt,
            ["tools"] = new JsonArray(new JsonObject
            {
                ["type"] = "web_search_20260209",
                ["name"] = "web_search",
                ["max_uses"] = 5,
            }),
            ["fallbacks"] = "default",
            ["messages"] = history,
        };

        JsonNode response;
        try
        {
            response = await Call(key, body).ConfigureAwait(false);
        }
        catch
        {
            Pop(); // keep the history consistent with what the model saw
            throw;
        }

        // A policy decline comes back as HTTP 200 with stop_reason "refusal".
        if (response.Str("stop_reason") == "refusal")
        {
            Pop();
            throw new UserFacingException(response.Get("stop_details").Str("explanation") ?? Loc.T("Claude declined this one."));
        }

        if (response.Get("content") is not JsonArray blocks)
        {
            Pop();
            throw new UserFacingException(Loc.T("Unexpected API response."));
        }

        // Store the whole content — tool_use / tool_result blocks included — so
        // the next turn has the right context.
        lock (gate) messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = blocks.DeepClone() });

        var reply = string.Join("\n", blocks
            .Where(b => b.Str("type") == "text")
            .Select(b => b.Str("text"))
            .OfType<string>()).Trim();

        return reply.Length == 0 ? throw new UserFacingException(Loc.T("No response text.")) : reply;
    }

    void Pop()
    {
        lock (gate)
        {
            if (messages.Count > 0) messages.RemoveAt(messages.Count - 1);
        }
    }

    static JsonObject Text(string text) => new() { ["type"] = "text", ["text"] = text };

    async Task<JsonNode> Call(string key, JsonObject body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Add("x-api-key", key);
        request.Headers.Add("anthropic-version", AnthropicVersion);
        request.Headers.Add("anthropic-beta", FallbackBeta);
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        string text;
        try
        {
            response = await http.SendAsync(request).ConfigureAwait(false);
            text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            throw new UserFacingException(Loc.T("Network error: {0}", e.Message));
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                // Surface the API's own message, which is what makes a bad key obvious.
                string? detail = null;
                try { detail = JsonNode.Parse(text).Get("error").Str("message"); }
                catch (JsonException) { }
                detail ??= text.Length > 200 ? text[..200] : text;
                var status = $"{(int)response.StatusCode} {response.ReasonPhrase ?? response.StatusCode.ToString()}";
                throw new UserFacingException(Loc.T("Claude API {0}: {1}", status, detail));
            }
            try
            {
                return JsonNode.Parse(text) ?? throw new JsonException("empty body");
            }
            catch (JsonException e)
            {
                throw new UserFacingException(Loc.T("Bad API response: {0}", e.Message));
            }
        }
    }

    /// <summary>
    /// PDF → document block, image → image block, text/code → inline text.
    /// Mirrors readFileAsBlock() in ClaudeService.swift.
    /// </summary>
    internal static JsonObject? FileBlock(string path)
    {
        (string Block, string Media)? kind = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".pdf" => ("document", "application/pdf"),
            ".jpg" or ".jpeg" => ("image", "image/jpeg"),
            ".png" => ("image", "image/png"),
            ".gif" => ("image", "image/gif"),
            ".webp" => ("image", "image/webp"),
            _ => null,
        };

        try
        {
            if (kind is { } found)
            {
                return new JsonObject
                {
                    ["type"] = found.Block,
                    ["source"] = new JsonObject
                    {
                        ["type"] = "base64",
                        ["media_type"] = found.Media,
                        ["data"] = Convert.ToBase64String(File.ReadAllBytes(path)),
                    },
                };
            }

            if (new FileInfo(path).Length > MaxInlineText) return null;
            // Strict UTF-8: a binary file is skipped, not sent as mojibake. Not
            // File.ReadAllText, which would take FF FE for a UTF-16 byte order mark.
            var text = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(File.ReadAllBytes(path));
            return Text($"File contents:\n{text}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            return null;
        }
    }
}
