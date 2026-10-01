// Named-pipe server for coucou-hook.
//
// `\\.\pipe\coucou-<sid>` — one instance per connection. Every hook event is
// forwarded to the island as a `hook` event. `PermissionRequest` is the only one
// that keeps its connection open: it waits for the island's decision and writes
// it back on the same pipe, which is how approving from the island works.
//
// Claude Code is never blocked by us. Three things guarantee it:
//   * coucou-hook gives the connection 300 ms and exits cleanly if we are closed;
//   * we only wait for a human once the island has *confirmed* the card is on
//     screen, so a paused island or a webview that is not listening costs a few
//     hundred milliseconds, not two minutes;
//   * whatever happens we drop the connection after the decision timeout, and
//     the terminal takes over.
//
// What we write back is the bare word `allow` or `deny`. Turning that into the
// documented hookSpecificOutput JSON is coucou-hook's job, so the wire format
// Claude Code expects lives in exactly one place.

using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;

namespace Coucou.Hooks;

/// <param name="emitHook">Hands an event to the island. Called from pool threads.</param>
sealed class HookRelay(Action<JsonObject> emitHook)
{
    /// <summary>Slightly under coucou-hook's own 110 s wait, so we always answer first.</summary>
    static readonly TimeSpan DecisionTimeout = TimeSpan.FromSeconds(108);

    /// <summary>
    /// How long the island gets to say "the card is up". Without it, an island
    /// that is paused, hidden behind a crashed webview or simply not listening
    /// would leave Claude Code staring at a prompt nobody can see for nearly two
    /// minutes.
    /// </summary>
    static readonly TimeSpan AckTimeout = TimeSpan.FromMilliseconds(800);

    const int MaxPayload = 1 << 20;

    /// <summary>What the island can say about a permission request.</summary>
    abstract record Reply
    {
        /// <summary>The card is on screen and a human can act on it.</summary>
        public sealed record Ack : Reply;

        /// <summary>A human clicked: <c>allow</c> or <c>deny</c>.</summary>
        public sealed record Decision(string Word) : Reply;

        /// <summary>Nobody can act on it — paused, or another request already holds the card.</summary>
        public sealed record Decline : Reply;
    }

    /// <summary>Permission requests the island has been told about.</summary>
    readonly ConcurrentDictionary<string, Channel<Reply>> pending = new();

    /// <summary>
    /// The window each session runs in (Claude Desktop, a terminal, an editor), as
    /// reported by the relay with the events that wait on the person.
    /// </summary>
    readonly ConcurrentDictionary<string, nint> hosts = new();

    /// <summary>The window session <paramref name="sessionId"/> runs in, if the relay said.</summary>
    public nint? HostOf(string sessionId) => hosts.TryGetValue(sessionId, out var hwnd) ? hwnd : null;

    long counter;

    public void Start() => _ = Task.Run(AcceptLoop);

    async Task AcceptLoop()
    {
        var name = RelayPipeName.Name;
        NamedPipeServerStream server;
        try
        {
            // FirstPipeInstance: we refuse to join a pipe somebody else already
            // owns under our name, rather than serving on top of it.
            server = CreateInstance(name, first: true);
        }
        catch (Exception e)
        {
            Log.Line($"cannot open the relay pipe: {e.Message}");
            return;
        }

        while (true)
        {
            try
            {
                await server.WaitForConnectionAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                await server.DisposeAsync().ConfigureAwait(false);
                await Task.Delay(200).ConfigureAwait(false);
                try { server = CreateInstance(name, first: false); }
                catch (Exception e)
                {
                    Log.Line($"cannot reopen the relay pipe: {e.Message}");
                    return;
                }
                continue;
            }

            // Hand the connected instance to a task and listen on a fresh one.
            var connected = server;
            try
            {
                server = CreateInstance(name, first: false);
            }
            catch (Exception e)
            {
                Log.Line($"cannot reopen the relay pipe: {e.Message}");
                _ = Task.Run(() => Handle(connected));
                return;
            }
            _ = Task.Run(() => Handle(connected));
        }
    }

    /// <remarks>
    /// CurrentUserOnly: the pipe's ACL admits this account only, so another user
    /// on the machine cannot even connect, let alone read tool calls.
    /// </remarks>
    static NamedPipeServerStream CreateInstance(string name, bool first) =>
        new(
            name,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly | (first ? PipeOptions.FirstPipeInstance : 0));

    async Task Handle(NamedPipeServerStream pipe)
    {
        await using var _ = pipe;

        var buffer = new MemoryStream();
        var chunk = new byte[4096];
        while (true)
        {
            int n;
            try
            {
                n = await pipe.ReadAsync(chunk).ConfigureAwait(false);
            }
            catch (IOException)
            {
                return;
            }
            if (n == 0) break;
            buffer.Write(chunk, 0, n);
            if (Array.IndexOf(chunk, (byte)'\n', 0, n) >= 0 || buffer.Length > MaxPayload) break;
        }

        if (ParseLine(buffer) is not { } payload) return;
        var hookEvent = (payload["hook_event_name"] as JsonValue)?.TryGetValue<string>(out var e) == true ? e : "";
        if (payload.Str("session_id") is { Length: > 0 } session && payload.Get("host_window").Int() is { } hwnd and > 0)
            hosts[session] = (nint)hwnd;

        if (hookEvent != "PermissionRequest")
        {
            Log.Line($"hook {hookEvent}");
            emitHook(payload);
            Disconnect(pipe);
            return;
        }

        var id = $"{Environment.ProcessId}-{Interlocked.Increment(ref counter)}";
        var replies = Channel.CreateBounded<Reply>(4);
        pending[id] = replies;
        payload["request_id"] = id;
        Log.Line($"hook PermissionRequest id={id}");
        emitHook(payload);

        var decision = await WaitForDecision(id, replies.Reader).ConfigureAwait(false);
        pending.TryRemove(id, out var _);

        // No decision: say nothing at all. coucou-hook then writes nothing to
        // stdout and Claude Code asks in the terminal, exactly as if Coucou were closed.
        if (decision is not null)
        {
            try
            {
                await pipe.WriteAsync(Encoding.UTF8.GetBytes(decision + "\n")).ConfigureAwait(false);
                await pipe.FlushAsync().ConfigureAwait(false);
            }
            catch (IOException)
            {
                // The relay already gave up; the terminal has the question.
            }
        }
        Disconnect(pipe);
    }

    static JsonObject? ParseLine(MemoryStream buffer)
    {
        var data = buffer.GetBuffer().AsSpan(0, (int)buffer.Length);
        var newline = data.IndexOf((byte)'\n');
        var line = newline >= 0 ? data[..newline] : data;
        try
        {
            return JsonNode.Parse(line) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static void Disconnect(NamedPipeServerStream pipe)
    {
        try { pipe.Disconnect(); }
        catch (Exception) { /* already gone */ }
    }

    /// <summary>Two waits: a short one for "the card is up", then the long one for a human.</summary>
    static async Task<string?> WaitForDecision(string id, ChannelReader<Reply> replies)
    {
        switch (await Receive(replies, AckTimeout).ConfigureAwait(false))
        {
            case Reply.Ack:
                break;
            // A click that beats the ack is still a click.
            case Reply.Decision d:
                Log.Line($"hook id={id} answered {d.Word}");
                return d.Word;
            case Reply.Decline:
                Log.Line($"hook id={id} not shown — terminal takes over");
                return null;
            default:
                Log.Line($"hook id={id} island never acknowledged — terminal takes over");
                return null;
        }

        switch (await Receive(replies, DecisionTimeout).ConfigureAwait(false))
        {
            case Reply.Decision d:
                Log.Line($"hook id={id} answered {d.Word}");
                return d.Word;
            case Reply.Decline:
                Log.Line($"hook id={id} released without a decision");
                return null;
            default:
                Log.Line($"hook id={id} timed out — terminal takes over");
                return null;
        }
    }

    static async Task<Reply?> Receive(ChannelReader<Reply> replies, TimeSpan timeout)
    {
        using var cancel = new CancellationTokenSource(timeout);
        try
        {
            return await replies.ReadAsync(cancel.Token).ConfigureAwait(false);
        }
        catch (Exception e) when (e is OperationCanceledException or ChannelClosedException)
        {
            return null;
        }
    }

    void Send(string requestId, Reply reply, bool keep)
    {
        var found = keep ? pending.TryGetValue(requestId, out var replies) : pending.TryRemove(requestId, out replies);
        if (found) replies!.Writer.TryWrite(reply);
        else Log.Line($"reply for id={requestId} — no pending request");
    }

    /// <summary>The island has the card on screen; the long wait may begin.</summary>
    public void Acknowledge(string requestId) => Send(requestId, new Reply.Ack(), keep: true);

    /// <summary>Nobody can act on this one — paused, or another card already holds the view.</summary>
    public void Decline(string requestId)
    {
        Log.Line($"decline id={requestId}");
        Send(requestId, new Reply.Decline(), keep: false);
    }

    /// <summary>
    /// Called by the island's Allow / Deny buttons. Only ever a bare word:
    /// turning it into Claude Code's JSON is coucou-hook's job.
    /// </summary>
    public void Answer(string requestId, string decision)
    {
        var word = decision is "allow" or "always" ? "allow" : "deny";
        Log.Line($"decision id={requestId} {word}");
        Send(requestId, new Reply.Decision(word), keep: false);
    }
}
