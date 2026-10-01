// Entry point: boot the bridge, wire the island, start the greeting.

import "./style.css";
import { Bridge, IN_APP, onEvent } from "./core/bridge";
import { language, resolveLanguage, setLanguage } from "./core/i18n";
import { Sound } from "./core/sound";
import { State, type Settings } from "./core/state";
import { Island } from "./island/island";
import { registerHookHandlers } from "./island/hooks";
import { registerIntegrationHandlers, refreshConfigured } from "./island/integrations";

/** Set across a reload made only to switch language: no second greeting. */
const RELAUNCH_KEY = "coucou-language-reload";

async function main() {
  const root = document.getElementById("root");
  if (!root) return;

  void Sound.preload();

  // Settings first: the views are built in the right language from the start.
  const boot = await Bridge.boot();
  if (boot) {
    State.settings = { ...State.settings, ...boot.settings };
  }
  // `npm run dev` in a browser: /?lang=it shows the island in another language.
  const devLang = IN_APP ? null : new URLSearchParams(location.search).get("lang");
  setLanguage(devLang ?? State.settings.language);

  const island = new Island(root);
  island.applySettings();
  State.loadIntegrationTasks();

  await onEvent<{ x: number; y: number }>("cursor", ({ x, y }) => island.onCursor(x, y));

  /** Pause has to reach the host too, or the pollers keep calling out. */
  const setPaused = (on: boolean) => {
    if (State.paused === on) return;
    State.paused = on;
    void Bridge.setPaused(on);
  };

  await onEvent<string>("tray", (what) => {
    switch (what) {
      case "settings":
        setPaused(false);
        island.alert("settings");
        break;
      case "open":
        setPaused(false);
        island.alert(State.defaultView());
        break;
      case "pause":
        setPaused(!State.paused);
        if (State.paused) island.fsm.forceHidden();
        else island.reveal();
        break;
    }
  });

  await onEvent<null>("screen-changed", () => void Bridge.reposition());

  // The settings window writes preferences; apply them here without a restart.
  await onEvent<Settings>("settings-changed", (s) => {
    State.settings = { ...State.settings, ...s };
    // Every view is built once, in one language: a new language means a fresh page.
    if (resolveLanguage(State.settings.language) !== language()) {
      try { sessionStorage.setItem(RELAUNCH_KEY, "1"); } catch { /* the greeting plays again, that's all */ }
      location.reload();
      return;
    }
    island.applySettings();
    State.loadIntegrationTasks();
    void refreshConfigured();
  });

  registerHookHandlers(island);
  registerIntegrationHandlers(island);

  let relaunch = false;
  try {
    relaunch = sessionStorage.getItem(RELAUNCH_KEY) === "1";
    sessionStorage.removeItem(RELAUNCH_KEY);
  } catch { /* no storage: treat it as a fresh start */ }

  if (relaunch) {
    // The window may have been down to the wake strip when the page reloaded.
    await Bridge.setCollapsed(false);
    island.fsm.forcePetit();
    // The cards' data lived in the old page; ask for it again rather than wait
    // for the next poll, which can be five minutes away.
    for (const id of State.settings.activeIntegrations) void Bridge.refreshIntegration(id);
  } else {
    island.launch();
  }

  // In a plain browser there is no wake strip behind the cursor: make the whole
  // page wake the island so the visuals can be checked with `npm run dev`.
  if (!IN_APP) {
    document.addEventListener("click", () => Sound.resume(), { once: true });
  }
}

void main();
