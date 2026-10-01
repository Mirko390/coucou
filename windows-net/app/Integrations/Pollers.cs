// Integration pollers — the C# side of StripePoller / GithubPoller /
// VercelPoller / N8nPoller / ResendPoller / NotionPoller / CalcomPoller.
//
// Same endpoints, same first-run delays and intervals as the Swift pollers. Each
// one emits an `integration` event; the island owns the badge, the sound and the
// 60 s auto-clear, exactly as the Swift handlers do.
//
// Nothing is polled until its key exists in the Credential Manager, and no
// request goes anywhere the user has not configured.

using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Coucou.Integrations;

/// <param name="emit">Hands an <c>integration</c> update to the island.</param>
/// <param name="enabled">True when the user has this integration switched on in settings.</param>
sealed class Pollers(Action<JsonObject> emit, Func<string, bool> enabled)
{
    readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(10) };

    /// <summary>Remembers the newest id per integration so an event fires once, not on every poll.</summary>
    readonly ConcurrentDictionary<string, string> seen = new();

    /// <summary>
    /// Set from the tray's Pause item. While it is on, nothing reaches the
    /// network: pausing Coucou has to mean pausing Coucou, not just hiding the island.
    /// </summary>
    public bool Paused
    {
        get => paused;
        set => paused = value;
    }

    volatile bool paused;

    /// <summary>Spawns every poller with the macOS delays and intervals.</summary>
    public void Start()
    {
        Spawn("integration_n8n", 3, 15, PollN8n);
        Spawn("integration_vercel", 5, 30, PollVercel);
        Spawn("integration_stripe", 6, 30, PollStripe);
        Spawn("integration_resend", 6, 60, PollResend);
        Spawn("integration_github", 7, 300, PollGithub);
        Spawn("integration_calcom", 8, 300, PollCalcom);
        Spawn("integration_notion", 9, 300, PollNotion);
    }

    void Spawn(string id, int delaySeconds, int everySeconds, Func<Task> poll) =>
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds)).ConfigureAwait(false);
            using var ticker = new PeriodicTimer(TimeSpan.FromSeconds(everySeconds));
            do
            {
                // The ticker keeps its cadence; we just decline to do the work. An
                // integration the user switched off, or a paused app, must make no
                // network calls at all — CLAUDE.md allows talking only to services
                // the user configured, and a disabled one is not configured.
                if (Paused || !enabled(id)) continue;
                await Guarded(id, poll).ConfigureAwait(false);
            }
            while (await ticker.WaitForNextTickAsync().ConfigureAwait(false));
        });

    static async Task Guarded(string id, Func<Task> poll)
    {
        try
        {
            await poll().ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Log.Line($"{id} poll failed: {e.GetType().Name}");
        }
    }

    /// <summary>One-shot refresh from the Refresh buttons in the island.</summary>
    public Task PollOnce(string id) => id switch
    {
        "integration_stripe" => Guarded(id, PollStripe),
        "integration_github" => Guarded(id, PollGithub),
        "integration_vercel" => Guarded(id, PollVercel),
        "integration_n8n" => Guarded(id, PollN8n),
        "integration_resend" => Guarded(id, PollResend),
        "integration_notion" => Guarded(id, PollNotion),
        "integration_calcom" => Guarded(id, PollCalcom),
        _ => Task.CompletedTask,
    };

    // ── Plumbing ──────────────────────────────────────────────────────────────

    /// <summary>
    /// What the island receives. <paramref name="change"/> is only set when
    /// something actually changed, which is what drives the pill badge and the sound.
    /// </summary>
    void Emit(string id, JsonObject data, string? error = null, JsonObject? change = null) =>
        emit(new JsonObject { ["id"] = id, ["data"] = data, ["error"] = error, ["event"] = change });

    static JsonObject Change(bool success, string label, string? detail = null) =>
        new() { ["success"] = success, ["label"] = label, ["detail"] = detail };

    /// <summary>
    /// True the first time a given id is seen — and false on the very first load,
    /// which only fills the card, like the Swift pollers.
    /// </summary>
    bool IsNew(string key, string id)
    {
        var known = seen.TryGetValue(key, out var previous);
        seen[key] = id;
        return known && previous != id;
    }

    static string StatusError(int code, string forbiddenHint) => code switch
    {
        401 => Loc.T("Invalid API key (401)"),
        403 => Loc.T(forbiddenHint),
        _ => Loc.T("API error {0}", code),
    };

    /// <summary>Sends, or returns null when the service could not be reached.</summary>
    async Task<HttpResponseMessage?> TrySend(HttpRequestMessage request)
    {
        try
        {
            return await http.SendAsync(request).ConfigureAwait(false);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    static HttpRequestMessage Request(string url, params (string Name, string Value)[] headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        foreach (var (name, value) in headers) request.Headers.TryAddWithoutValidation(name, value);
        return request;
    }

    /// <summary>The body as JSON, or an empty object when it is not JSON.</summary>
    static async Task<JsonNode> ReadJson(HttpResponseMessage response)
    {
        try
        {
            return JsonNode.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false)) ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    static JsonArray ToArray(IEnumerable<JsonNode?> items) => new(items.ToArray());

    // ── Stripe ────────────────────────────────────────────────────────────────

    async Task PollStripe()
    {
        if (Secrets.Get("stripe-api-key") is not { } key) return;
        var auth = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{key}:"));
        const string id = "integration_stripe";

        long amount;
        string currency;
        try
        {
            using var balance = await http.SendAsync(Request("https://api.stripe.com/v1/balance", ("Authorization", auth)))
                .ConfigureAwait(false);
            if (!balance.IsSuccessStatusCode)
            {
                Emit(id, [], StatusError((int)balance.StatusCode, "Use a secret key (sk_live_… not pk_live_…)"));
                return;
            }
            var json = await ReadJson(balance).ConfigureAwait(false);
            var buckets = new[] { "available", "pending" }
                .SelectMany(k => json.Get(k).Arr() ?? [])
                .ToList();
            currency = buckets.FirstOrDefault().Str("currency") ?? "eur";
            amount = buckets.Sum(b => b.Get("amount").Int() ?? 0);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            Emit(id, [], Loc.T("No connection: {0}", e.Message));
            return;
        }

        using var charges = await TrySend(Request("https://api.stripe.com/v1/charges?limit=3", ("Authorization", auth)))
            .ConfigureAwait(false);
        if (charges is null || !charges.IsSuccessStatusCode) return;
        var list = await ReadJson(charges).ConfigureAwait(false);

        var payments = new List<JsonObject>();
        foreach (var c in list.Get("data").Arr() ?? [])
        {
            if (c.Str("id") is not { } chargeId || c.Get("amount").Int() is not { } cents || c.Str("currency") is not { } cur)
                continue;
            payments.Add(new JsonObject
            {
                ["id"] = chargeId,
                ["amount"] = cents,
                ["currency"] = cur,
                ["description"] = c.Str("description") ?? c.Get("billing_details").Str("name"),
                ["createdAt"] = (c.Get("created").Int() ?? 0) * 1000,
                ["status"] = c.Str("status") ?? "succeeded",
            });
        }

        JsonObject? change = null;
        var newest = payments.FirstOrDefault().Str("id") ?? "";
        if (newest.Length > 0 && IsNew("stripe", newest))
        {
            var label = payments[0].Str("description")
                ?? ((payments[0].Get("amount").Int() ?? 0) / 100.0).ToString("F2", CultureInfo.InvariantCulture);
            change = Change(true, label);
        }

        Emit(id, new JsonObject { ["balance"] = amount, ["currency"] = currency, ["payments"] = ToArray(payments) },
            change: change);
    }

    // ── GitHub ────────────────────────────────────────────────────────────────

    async Task PollGithub()
    {
        if (Secrets.Get("github-token") is not { } token) return;
        (string, string)[] headers =
        [
            ("Authorization", $"Bearer {token}"),
            ("Accept", "application/vnd.github+json"),
            ("User-Agent", "Coucou"),
        ];

        using var user = await TrySend(Request("https://api.github.com/user", headers)).ConfigureAwait(false);
        if (user is null) return;
        if (!user.IsSuccessStatusCode)
        {
            Emit("integration_github", [], StatusError((int)user.StatusCode, "Token lacks the needed scope"));
            return;
        }
        var json = await ReadJson(user).ConfigureAwait(false);
        var publicRepos = json.Get("public_repos").Int() ?? 0;
        var privateRepos = (json.Get("owned_private_repos") ?? json.Get("total_private_repos")).Int() ?? 0;

        long stars = 0;
        using var repos = await TrySend(Request(
            "https://api.github.com/user/repos?per_page=100&affiliation=owner&sort=pushed", headers)).ConfigureAwait(false);
        if (repos is { IsSuccessStatusCode: true })
        {
            var list = await ReadJson(repos).ConfigureAwait(false);
            stars = (list.Arr() ?? []).Sum(r => r.Get("stargazers_count").Int() ?? 0);
        }

        Emit("integration_github", new JsonObject { ["totalRepos"] = publicRepos + privateRepos, ["totalStars"] = stars });
    }

    // ── Vercel ────────────────────────────────────────────────────────────────

    async Task PollVercel()
    {
        if (Secrets.Get("vercel-token") is not { } token) return;
        using var response = await TrySend(Request(
            "https://api.vercel.com/v6/deployments?limit=5",
            ("Authorization", $"Bearer {token}"),
            ("Accept", "application/json"))).ConfigureAwait(false);
        if (response is null) return;
        if (!response.IsSuccessStatusCode)
        {
            Emit("integration_vercel", [], StatusError((int)response.StatusCode, "Token lacks access"));
            return;
        }
        var json = await ReadJson(response).ConfigureAwait(false);
        string[] terminal = ["READY", "ERROR", "CANCELED"];

        var deployments = new List<JsonObject>();
        foreach (var d in json.Get("deployments").Arr() ?? [])
        {
            if (d.Str("state") is not { } state || !terminal.Contains(state)) continue;
            if (d.Str("uid") is not { } uid || d.Str("name") is not { } projectName) continue;
            var meta = d.Get("meta");
            string? Pick(params string[] keys) => keys.Select(meta.Str).FirstOrDefault(v => v is not null);
            deployments.Add(new JsonObject
            {
                ["id"] = uid,
                ["projectName"] = projectName,
                ["url"] = d.Str("url") ?? "",
                ["state"] = state,
                ["createdAt"] = d.Get("createdAt").Num() ?? 0.0,
                ["commitMessage"] = Pick("githubCommitMessage", "gitlabCommitMessage", "bitbucketCommitMessage"),
                ["branch"] = Pick("githubCommitRef", "gitlabCommitRef", "bitbucketBranch"),
            });
        }

        JsonObject? change = null;
        if (deployments.FirstOrDefault() is { } latest && IsNew("vercel", latest.Str("id")!))
            change = Change(latest.Str("state") == "READY", latest.Str("projectName")!);

        Emit("integration_vercel", new JsonObject { ["deployments"] = ToArray(deployments) }, change: change);
    }

    // ── Resend ────────────────────────────────────────────────────────────────

    async Task PollResend()
    {
        if (Secrets.Get("resend-api-key") is not { } key) return;
        using var response = await TrySend(Request(
            "https://api.resend.com/emails?limit=100",
            ("Authorization", $"Bearer {key}"),
            ("Accept", "application/json"))).ConfigureAwait(false);
        if (response is null) return;
        if (!response.IsSuccessStatusCode)
        {
            Emit("integration_resend", [], StatusError((int)response.StatusCode, "Key lacks access"));
            return;
        }
        var json = await ReadJson(response).ConfigureAwait(false);
        var total = (json.Get("total") ?? json.Get("count")).Int();

        var emails = new List<JsonObject>();
        foreach (var e in (json.Get("data").Arr() ?? []).Take(5))
        {
            if (e.Str("id") is not { } emailId) continue;
            var to = e.Get("to") switch
            {
                JsonArray list => list.DeepClone().AsArray(),
                JsonValue v when v.Str() is { } single => new JsonArray(single),
                _ => [],
            };
            emails.Add(new JsonObject
            {
                ["id"] = emailId,
                ["to"] = to,
                ["subject"] = e.Str("subject") ?? "",
                ["createdAt"] = e.Str("created_at") ?? "",
                ["lastEvent"] = e.Str("last_event") ?? "",
            });
        }

        Emit("integration_resend", new JsonObject { ["emails"] = ToArray(emails), ["total"] = total });
    }

    // ── Notion ────────────────────────────────────────────────────────────────

    async Task PollNotion()
    {
        if (Secrets.Get("notion-api-key") is not { } token) return;
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.notion.com/v1/search")
        {
            Content = new StringContent(
                """{"sort":{"direction":"descending","timestamp":"last_edited_time"},"page_size":3}""",
                new MediaTypeHeaderValue("application/json")),
        };
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
        request.Headers.TryAddWithoutValidation("Notion-Version", "2022-06-28");

        using var response = await TrySend(request).ConfigureAwait(false);
        if (response is null) return;
        if (!response.IsSuccessStatusCode)
        {
            Emit("integration_notion", [], StatusError((int)response.StatusCode, "Integration lacks access"));
            return;
        }
        var json = await ReadJson(response).ConfigureAwait(false);
        var pages = (json.Get("results").Arr() ?? []).Select(ParseNotionPage).OfType<JsonObject>();
        Emit("integration_notion", new JsonObject { ["pages"] = ToArray(pages) });
    }

    internal static JsonObject? ParseNotionPage(JsonNode? obj)
    {
        if (obj.Str("id") is not { } id) return null;
        var isDatabase = obj.Str("object") == "database";

        var title = "Untitled";
        if (isDatabase)
        {
            if (obj.Get("title").Arr()?.FirstOrDefault().Str("plain_text") is { Length: > 0 } text) title = text;
        }
        else if (obj.Get("properties").Obj() is { } props)
        {
            foreach (var (_, prop) in props)
            {
                if (prop.Str("type") != "title") continue;
                if (prop.Get("title").Arr()?.FirstOrDefault().Str("plain_text") is { Length: > 0 } text)
                {
                    title = text;
                    break;
                }
            }
        }

        var icon = obj.Get("icon");
        var emoji = icon.Str("type") == "emoji" ? icon.Str("emoji") : null;

        if (obj.Str("last_edited_time") is not { } lastEdited) return null;
        return new JsonObject
        {
            ["id"] = id,
            ["title"] = title,
            ["emoji"] = emoji,
            ["lastEditedAt"] = lastEdited,
            ["url"] = obj.Str("url") ?? "https://notion.so",
        };
    }

    // ── Cal.com ───────────────────────────────────────────────────────────────

    async Task PollCalcom()
    {
        if (Secrets.Get("calcom-api-key") is not { } key) return;
        using var response = await TrySend(Request(
            "https://api.cal.com/v2/bookings?status=upcoming",
            ("Authorization", $"Bearer {key}"),
            ("cal-api-version", "2024-08-13"))).ConfigureAwait(false);
        if (response is null) return;
        if (!response.IsSuccessStatusCode)
        {
            Emit("integration_calcom", [], StatusError((int)response.StatusCode, "Key lacks access"));
            return;
        }
        var json = await ReadJson(response).ConfigureAwait(false);

        var bookings = new List<JsonObject>();
        foreach (var b in json.Get("data").Arr() ?? [])
        {
            if ((b.Str("start") ?? b.Str("startTime")) is not { } start) continue;
            var attendee = b.Get("attendees").Arr()?.FirstOrDefault();
            var notes = b.Get("responses").Get("notes").Str("value") ?? b.Str("description");
            bookings.Add(new JsonObject
            {
                // Like serde's Value::to_string: numbers bare, strings quoted.
                ["id"] = b.Get("id")?.ToJsonString() ?? "",
                ["title"] = b.Str("title") ?? "Meeting",
                ["start"] = start,
                ["status"] = b.Str("status") ?? "accepted",
                ["attendeeName"] = attendee.Str("name"),
                ["attendeeEmail"] = attendee.Str("email"),
                ["attendeeNotes"] = string.IsNullOrEmpty(notes) ? null : notes,
            });
        }

        Emit("integration_calcom", new JsonObject { ["bookings"] = ToArray(bookings) });
    }

    // ── n8n ───────────────────────────────────────────────────────────────────

    async Task PollN8n()
    {
        if (Secrets.Get("n8n-api-key") is not { } key || Secrets.Get("n8n-url") is not { } rawBase) return;
        var baseUrl = rawBase.TrimEnd('/');
        (string, string)[] headers = [("X-N8N-API-KEY", key), ("Accept", "application/json")];

        // Same two shapes as the Swift poller: the public API first, then /rest.
        string[] listUrls =
        [
            $"{baseUrl}/api/v1/executions?limit=1&includeData=false",
            $"{baseUrl}/rest/executions?limit=1&includeData=false",
        ];

        JsonArray? items = null;
        foreach (var url in listUrls)
        {
            using var response = await TrySend(Request(url, headers)).ConfigureAwait(false);
            if (response is null) continue;
            if (!response.IsSuccessStatusCode)
            {
                // Only the status: a self-hosted base URL can carry credentials.
                Log.Line($"n8n list HTTP {(int)response.StatusCode}");
                continue;
            }
            var json = await ReadJson(response).ConfigureAwait(false);
            items = json switch
            {
                JsonObject o => o.Get("data").Arr(),
                JsonArray a => a,
                _ => null,
            };
            if (items is not null) break;
        }

        if (items?.FirstOrDefault() is not { } first) return;
        var id = first.Get("id") switch
        {
            JsonValue v when v.Str() is { } s => s,
            JsonValue v when v.GetValueKind() == JsonValueKind.Number => v.ToJsonString(),
            _ => null,
        };
        if (id is null) return;

        var status = first.Str("status") ?? "";
        if (!new[] { "success", "error", "crashed", "canceled", "failed" }.Contains(status)) return;
        if (!IsNew("n8n", id)) return;
        var success = status == "success";

        string[] detailUrls =
        [
            $"{baseUrl}/api/v1/executions/{id}?includeData=true",
            $"{baseUrl}/api/v1/executions/{id}",
            $"{baseUrl}/rest/executions/{id}?includeData=true",
            $"{baseUrl}/rest/executions/{id}",
        ];
        var name = "Workflow";
        string? detail = null;
        foreach (var url in detailUrls)
        {
            using var response = await TrySend(Request(url, headers)).ConfigureAwait(false);
            if (response is not { IsSuccessStatusCode: true }) continue;
            JsonNode json;
            try
            {
                json = JsonNode.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false)) ?? new JsonObject();
            }
            catch (JsonException)
            {
                continue;
            }
            name = json.Get("workflowData").Str("name") ?? json.Str("name") ?? "Workflow";
            detail = N8nDetail(json, success);
            break;
        }

        Log.Line($"n8n execution {id} {status} · {name}");
        Emit("integration_n8n", new JsonObject { ["workflow"] = name, ["status"] = status },
            change: Change(success, name, detail));
    }

    internal static string? N8nDetail(JsonNode json, bool success)
    {
        var result = json.Get("data").Get("resultData");
        if (result is null) return null;

        if (!success)
        {
            if (result.Get("error") is { } error)
            {
                var message = error.Str("message") ?? "";
                return error.Get("node").Str("name") is { Length: > 0 } node ? $"{node}\n{message}" : message;
            }
            if (result.Get("runData").Obj() is not { } runs) return null;
            foreach (var (node, value) in runs)
            {
                if (value.Arr()?.FirstOrDefault().Get("error").Str("message") is { } message)
                    return $"{node}\n{message}";
            }
            return null;
        }

        if (result.Str("lastNodeExecuted") is not { } lastNode) return null;
        var items = result.Get("runData").Get(lastNode).Arr()?.FirstOrDefault()
            .Get("data").Get("main").Arr()?.FirstOrDefault().Arr();
        if (items is null) return null;

        var count = items.Count;
        var header = $"→ {lastNode} · {count} item{(count == 1 ? "" : "s")}";
        var fields = items.FirstOrDefault().Get("json").Obj() is { } obj
            ? string.Join("\n", obj.Take(4).Select(p => $"{p.Key}: {FormatValue(p.Value)}"))
            : "";
        return fields.Length > 0 ? $"{header}\n{fields}" : header;
    }

    static string FormatValue(JsonNode? value) => value switch
    {
        JsonValue v when v.Str() is { } s => string.Concat(s.EnumerateRunes().Take(50).Select(r => r.ToString())),
        JsonArray a => $"[{a.Count}]",
        JsonObject => "{…}",
        null => "null",
        _ => value.ToJsonString(),
    };
}
