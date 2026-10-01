// Checks the translation tables against the code: every literal passed to t()
// must have an entry in each language, and no entry may be left over.
//
//   node scripts/check-i18n.mjs        (also run by `npm run build`)

import { readFileSync, readdirSync, statSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const src = join(dirname(fileURLToPath(import.meta.url)), "..", "src");

/** Labels that reach t() through a variable rather than a literal. */
const INDIRECT = [
  // Claude Code step verbs (island/hooks.ts TOOL_LABELS)
  "Runs", "Reads", "Writes", "Edits", "Finds", "Searches", "Web search", "Fetches", "Tasks", "Agent", "Lists",
  // Integration key fields (settings/main.ts INTEGRATIONS)
  "Secret key", "Instance URL", "API key", "Integration token",
  // Drop zone tags (views/upload.ts)
  "Images", "Code", "Docs",
];

/** Strings deliberately the same in every language. */
const UNTRANSLATED = new Set(["PDF", "Token", "Notebook", "OK", "Workflow"]);

const files = [];
(function walk(dir) {
  for (const name of readdirSync(dir)) {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) walk(path);
    else if (path.endsWith(".ts")) files.push(path);
  }
})(src);

const unquote = (s) => JSON.parse(`"${s}"`);
const used = new Set(INDIRECT);
for (const file of files) {
  const text = readFileSync(file, "utf8");
  for (const m of text.matchAll(/\bt\(\s*"((?:[^"\\]|\\.)*)"/g)) used.add(unquote(m[1]));
}

const i18n = readFileSync(join(src, "core", "i18n.ts"), "utf8");
const tables = [...i18n.matchAll(/^const ([A-Z]{2}): Record<string, string> = \{([\s\S]*?)^\};/gm)];
let problems = 0;
for (const [, name, body] of tables) {
  const keys = new Set([...body.matchAll(/^\s*"((?:[^"\\]|\\.)*)":/gm)].map((m) => unquote(m[1])));
  const missing = [...used].filter((k) => !keys.has(k) && !UNTRANSLATED.has(k));
  const unused = [...keys].filter((k) => !used.has(k));
  for (const k of missing) console.error(`${name}: missing "${k}"`);
  for (const k of unused) console.error(`${name}: unused "${k}"`);
  problems += missing.length + unused.length;
  console.log(`${name}: ${keys.size} entries, ${used.size} strings in the code`);
}
if (tables.length === 0) {
  console.error("no translation table found in core/i18n.ts");
  problems++;
}
process.exit(problems ? 1 : 0);
