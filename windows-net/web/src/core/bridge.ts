// Thin wrapper over the host's commands/events, carried by WebView2's
// `chrome.webview` channel. Every call is a no-op when the page is opened in a
// plain browser, so the island can be iterated on with `npm run dev` alone.
//
// Wire format (the C# side lives in app/Web/WebHost.cs):
//   page → host   { id, cmd, args }
//   host → page   { id, ok: true, result } | { id, ok: false, error }
//   host → page   { event, payload }

import type { Settings } from "./state";

interface HostChannel {
  postMessage(message: unknown): void;
  postMessageWithAdditionalObjects(message: unknown, objects: ArrayLike<unknown>): void;
  addEventListener(type: "message", listener: (e: MessageEvent) => void): void;
}

const host: HostChannel | undefined =
  typeof window !== "undefined"
    ? (window as unknown as { chrome?: { webview?: HostChannel } }).chrome?.webview
    : undefined;

/** True inside Coucou, false in an ordinary browser tab. */
export const IN_APP = host !== undefined;

type Reply = { id: number; ok: boolean; result?: unknown; error?: string };
type Pending = { resolve: (value: unknown) => void; reject: (reason: unknown) => void };

const pending = new Map<number, Pending>();
const listeners = new Map<string, Set<(payload: unknown) => void>>();
let nextId = 1;

host?.addEventListener("message", (e) => {
  const message = e.data as Partial<Reply> & { event?: string; payload?: unknown };
  if (typeof message?.id === "number") {
    const waiting = pending.get(message.id);
    if (!waiting) return;
    pending.delete(message.id);
    // Rejected with the bare message, as Tauri did: the views print String(err).
    if (message.ok) waiting.resolve(message.result ?? null);
    else waiting.reject(message.error ?? "unknown error");
  } else if (typeof message?.event === "string") {
    for (const fn of listeners.get(message.event) ?? []) fn(message.payload ?? null);
  }
});

function invoke<T>(cmd: string, args?: Record<string, unknown>, objects?: ArrayLike<unknown>): Promise<T> {
  if (!host) return Promise.reject("not running inside Coucou");
  const id = nextId++;
  return new Promise<T>((resolve, reject) => {
    pending.set(id, { resolve: resolve as (value: unknown) => void, reject });
    const message = { id, cmd, args: args ?? {} };
    if (objects) host.postMessageWithAdditionalObjects(message, objects);
    else host.postMessage(message);
  });
}

async function call<T>(cmd: string, args?: Record<string, unknown>): Promise<T | null> {
  if (!IN_APP) return null;
  try {
    return await invoke<T>(cmd, args);
  } catch (err) {
    console.error(`[coucou] ${cmd} failed`, err);
    return null;
  }
}

export interface BootInfo {
  settings: Settings;
  /** Logical screen rect of the monitor the island lives on. */
  screen: { x: number; y: number; width: number; height: number; scale: number };
  version: string;
  hookPath: string;
}

export const Bridge = {
  boot: () => call<BootInfo>("boot"),

  saveSettings: (settings: Settings) => call<void>("save_settings", { settings }),

  /** Shrink the window down to the invisible wake strip (hidden) or back to full. */
  setCollapsed: (collapsed: boolean) => call<void>("set_collapsed", { collapsed }),

  /**
   * Pushes the island shape in window coordinates. The host flips click-through
   * from its own cursor poll, so the flag is never a frame behind a click.
   */
  setIslandRect: (x: number, y: number, width: number, height: number, margin: number) =>
    call<void>("set_island_rect", { x, y, width, height, margin }),

  /** Give the window keyboard focus (chat field) and take it away again. */
  focusWindow: (focused: boolean) => call<void>("focus_window", { focused }),

  reposition: () => call<void>("reposition"),

  openUrl: (url: string) => call<void>("open_url", { url }),

  /** The session's folder in the editor picked in settings (Visual Studio, VS Code, Explorer). */
  openProject: (path: string | null) => call<boolean>("open_project", { path }),

  quit: () => call<void>("quit_app"),

  openSettingsWindow: () => call<void>("open_settings_window"),

  /** Writes to %LOCALAPPDATA%\Coucou\coucou.log, next to the host's own lines. */
  log: (message: string) => call<void>("log_line", { message }),

  // ── Claude Code hooks ─────────────────────────────────────────────────────
  hooksStatus: () => call<HookStatus>("hooks_status"),
  /** Diff to show before anything is written. `install: false` previews removal. */
  hooksPreview: (install: boolean) => callOrThrow<HookPreview>("hooks_preview", { install }),
  /**
   * Writes ~/.claude/settings.json — only ever after an explicit click, and only
   * when the file still matches the preview the user looked at.
   */
  hooksApply: (install: boolean, fingerprint: string) =>
    callOrThrow<string>("hooks_apply", { install, fingerprint }),

  approvalDecision: (requestId: string, decision: "allow" | "deny") =>
    call<void>("approval_decision", { requestId, decision }),
  /** "The card is up" — until this lands the relay only waits a moment. */
  approvalAck: (requestId: string) => call<void>("approval_ack", { requestId }),
  /** "Nobody can act on this" — Claude Code asks in the terminal right away. */
  approvalDecline: (requestId: string) => call<void>("approval_decline", { requestId }),
  /** Brings forward the window a session runs in (Claude Desktop, a terminal…). */
  focusSession: (sessionId: string) => call<boolean>("focus_session", { sessionId }),

  // ── Chat, files, secrets ──────────────────────────────────────────────────
  /** One chat turn. The API key and any file bytes never leave the host. */
  chatSend: (query: string, context: ChatContext | null) =>
    callOrThrow<{ text: string }>("chat_send", { query, context }),
  chatReset: () => call<void>("chat_reset"),
  /** Copies a dropped file into the inbox. */
  ingestFile: (path: string) => callOrThrow<DroppedFile>("ingest_file", { path }),
  /** Only ever tells you whether a key exists — never its value. */
  secretPresent: (key: string) => call<boolean>("secret_present", { key }),
  secretSet: (key: string, value: string) => callOrThrow<void>("secret_set", { key, value }),
  secretClear: (key: string) => callOrThrow<void>("secret_clear", { key }),

  // ── Integrations ──────────────────────────────────────────────────────────
  refreshIntegration: (id: string) => call<void>("refresh_integration", { id }),
  /** Opens the configured n8n instance in the browser. */
  openN8n: () => call<void>("open_n8n"),

  /** Tray → Pause. Stops the integration pollers, not just the island. */
  setPaused: (paused: boolean) => call<void>("set_paused", { paused }),
};

export interface IntegrationUpdate {
  id: string;
  data: Record<string, unknown>;
  error: string | null;
  event: { success: boolean; label: string; detail: string | null } | null;
}

export type ChatContext =
  | { kind: "file"; name: string; path: string }
  | { kind: "window"; appName: string; title: string; url?: string };

export interface DroppedFile {
  name: string;
  path: string;
  size: number;
}

export interface HookStatus {
  installed: boolean;
  settingsPath: string;
  hookPath: string;
  hookReady: boolean;
}

export interface HookPreview {
  diff: string;
  backup: string;
  settingsPath: string;
  /** Hand back to hooksApply so only the reviewed diff is ever written. */
  fingerprint: string;
}

/** Same as `call`, but surfaces the error so the UI can show what went wrong. */
function callOrThrow<T>(cmd: string, args?: Record<string, unknown>): Promise<T> {
  return invoke<T>(cmd, args);
}

export type BridgeEvent =
  | { name: "cursor"; payload: { x: number; y: number } }
  | { name: "tray"; payload: string }
  | { name: "hook"; payload: Record<string, unknown> }
  | { name: "screen-changed"; payload: null };

export interface DragDropPayload {
  type: "enter" | "over" | "drop" | "leave";
  paths?: string[];
}

/**
 * Files dragged onto the island. Only reaches us when the window takes the mouse.
 *
 * WebView2 runs the drag as an ordinary HTML5 one, which gives the page File
 * objects but no paths. The paths come back from the host: the dropped Files are
 * handed over with postMessageWithAdditionalObjects, which WebView2 turns into
 * CoreWebView2File objects that carry the real path on disk.
 */
export async function onDragDrop(handler: (e: DragDropPayload) => void) {
  if (!IN_APP) return () => {};

  const carriesFiles = (e: DragEvent) => e.dataTransfer?.types.includes("Files") ?? false;
  // dragenter/dragleave fire for every element the pointer crosses; only the
  // outermost pair means the drag entered or left the window.
  let depth = 0;

  const enter = (e: DragEvent) => {
    if (!carriesFiles(e)) return;
    e.preventDefault();
    if (depth++ === 0) handler({ type: "enter" });
  };
  const over = (e: DragEvent) => {
    if (!carriesFiles(e)) return;
    // Without this WebView2 would open the file in place of the island.
    e.preventDefault();
    if (e.dataTransfer) e.dataTransfer.dropEffect = "copy";
    handler({ type: "over" });
  };
  const leave = (e: DragEvent) => {
    if (!carriesFiles(e)) return;
    depth = Math.max(0, depth - 1);
    if (depth === 0) handler({ type: "leave" });
  };
  const drop = (e: DragEvent) => {
    if (!carriesFiles(e)) return;
    e.preventDefault();
    depth = 0;
    const files = Array.from(e.dataTransfer?.files ?? []);
    if (files.length === 0) {
      handler({ type: "drop", paths: [] });
      return;
    }
    invoke<string[]>("dropped_paths", undefined, files)
      .then((paths) => handler({ type: "drop", paths }))
      .catch(() => handler({ type: "drop", paths: [] }));
  };

  window.addEventListener("dragenter", enter);
  window.addEventListener("dragover", over);
  window.addEventListener("dragleave", leave);
  window.addEventListener("drop", drop);
  return () => {
    window.removeEventListener("dragenter", enter);
    window.removeEventListener("dragover", over);
    window.removeEventListener("dragleave", leave);
    window.removeEventListener("drop", drop);
  };
}

// In a plain browser (`npm run dev`) there is no host to push events, so the
// console stands in for it:
//   __coucouEmit("hook", { hook_event_name: "PreToolUse", tool_name: "Bash", tool_input: { command: "ls" } })
if (!IN_APP && typeof window !== "undefined") {
  (window as unknown as { __coucouEmit: (event: string, payload?: unknown) => void }).__coucouEmit = (event, payload) => {
    for (const fn of listeners.get(event) ?? []) fn(payload ?? null);
  };
}

export async function onEvent<T>(name: string, handler: (payload: T) => void) {
  const set = listeners.get(name) ?? new Set();
  listeners.set(name, set);
  const fn = handler as (payload: unknown) => void;
  set.add(fn);
  return () => {
    set.delete(fn);
  };
}
