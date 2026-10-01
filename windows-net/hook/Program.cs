// coucou-hook — the relay Claude Code runs on every hook event.
//
// Reads the hook JSON on stdin, adds a little terminal context, and hands it to
// Coucou over the named pipe `\\.\pipe\coucou-<sid>`.
//
// Hard rule (CLAUDE.md): **never block Claude Code.**
// * If the pipe does not exist — Coucou is closed — we exit 0 immediately with
//   nothing on stdout, and the session carries on untouched.
// * Every step runs under a deadline enforced by the main thread, so a pipe that
//   accepts the connection and then stops reading cannot wedge the session
//   either: we abandon the worker and exit.
// * Only `PermissionRequest` waits for an answer, because approving from the
//   island is the whole point. No answer means empty stdout, and Claude Code
//   asks in the terminal exactly as if Coucou were not installed.
//
// Usage: `coucou-hook <EventName>` (the name is also read from the JSON).

using System.Text;
using Coucou.Hook;

// Whole-run budget for an event nobody waits on: connect and write, no more.
var fireAndForgetBudget = TimeSpan.FromSeconds(2);
// How long a permission prompt may stay on screen before the terminal takes over.
var decisionBudget = TimeSpan.FromSeconds(110);

HookEvent? hookEvent;
try
{
    using var stdin = Console.OpenStandardInput();
    using var buffer = new MemoryStream();
    stdin.CopyTo(buffer);
    hookEvent = HookPayload.Read(
        buffer.GetBuffer().AsSpan(0, (int)buffer.Length),
        args.FirstOrDefault() ?? "",
        Environment.GetEnvironmentVariable,
        Environment.CurrentDirectory,
        HostWindow.Find);
}
catch
{
    hookEvent = null;
}
if (hookEvent is null) return 0;

var waitsForAnswer = hookEvent.Event == "PermissionRequest";

// The worker owns every blocking call. If it overruns the budget we simply stop
// listening and exit: the process dying takes the pipe handle with it.
string? decision = null;
var worker = new Thread(() =>
{
    try { decision = Talk(hookEvent.Line, waitsForAnswer); }
    catch { decision = null; }
})
{ IsBackground = true };
worker.Start();

if (worker.Join(waitsForAnswer ? decisionBudget : fireAndForgetBudget)
    && decision is not null
    && HookPayload.DecisionJson(decision) is { } json)
{
    using var stdout = Console.OpenStandardOutput();
    stdout.Write(Encoding.UTF8.GetBytes(json + "\n"));
    stdout.Flush();
}
// Nothing printed: Claude Code asks in the terminal, as if we were not here.
Environment.Exit(0);
return 0;

// Connect, send, and — for a permission request — wait for the island's word.
static string? Talk(string payload, bool waitsForAnswer)
{
    using var pipe = RelayClient.Connect();
    if (pipe is null) return null;

    pipe.Write(Encoding.UTF8.GetBytes(payload));
    pipe.Flush();
    if (!waitsForAnswer) return null;

    var received = new List<byte>();
    var chunk = new byte[1024];
    while (true)
    {
        int n;
        try { n = pipe.Read(chunk); }
        catch (IOException) { break; }
        if (n == 0) break;
        received.AddRange(chunk.AsSpan(0, n));
        if (chunk.AsSpan(0, n).Contains((byte)'\n')) break;
    }
    var answer = Encoding.UTF8.GetString(received.ToArray()).Trim();
    return answer.Length == 0 ? null : answer;
}
