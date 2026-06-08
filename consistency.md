# rig — cross-implementation consistency

Comparison of the .NET (`dotnet/`) and Node (`node/`) implementations, focused on
CLI surface, the interactive menu, and command semantics. Verified against source.

What's already aligned: both have `--dry-run/-n`, `--quiet/-q`, `--no-env`; the same
`→ / ✓ / ! / ✗` glyphs and cyan/green/yellow/red palette; a shared alias core
(`b t fmt k i def od rb c`); `← back` rendering in menus; and a unified config schema
(shared top-level keys, `dotnet.*` / `node.*` namespaces).

## 1. Command consistency

### Real gaps (parity matrix claims ✅ but isn't)
- **`publish` doesn't exist in Node.** [ROADMAP.md](ROADMAP.md) claims parity, but there is
  no `publish` verb in [node/src/commands.ts](node/src/commands.ts). Either build it or
  correct the matrix.
- ✅ ~~**`completion`** is a real verb in .NET (`completion`/`comp`, prints zsh/bash/pwsh
  setup); Node leans on gunshi's built-in with no discoverable verb.~~ Resolved: Node now has
  a native `completion` verb, and both tools share one `[suggest:N] "<line>"` protocol (a plain
  newline list). The generated script is identical in shape, so a single completer works no
  matter which `rig` wins on PATH — a .NET dir forwards to the .NET tool, a Node dir to the Node
  one. Replaced gunshi's completion plugin (which wasn't emitting candidates here).
- ✅ ~~**`--version` short form.** .NET adds `-v`; Node only has `--version`.~~
  Already satisfied — gunshi's built-in `version` option declares `short: "v"`, so `rig -v` /
  `rignode -v` already print the version (verified). The original audit missed this. Note: `-v`
  is therefore reserved for version in both tools, so a future `--verbose` can't claim it.

### Flag-surface mismatches
- **`coverage --full`** exists in the .NET CLI and in *both* config schemas (`coverage.full`),
  but Node has no `--full` flag — the config key is unreachable from Node's CLI. Add the flag.
- **`add` targets the project differently.** .NET: `add [package] --project/-p`.
  Node: `add <package> [project]` positional + `--dev/-D`. Pick one convention.
  (`--dev` is genuinely N/A for NuGet, but the project-targeting style should match.)
- ✅ ~~**`kill`** is port-aware in Node (`--port`, repeatable) but not in .NET.~~
  .NET `kill` now takes `--port N` (repeatable), and a bare numeric arg (`rig kill 3000`) is
  treated as a port, matching Node. Frees listeners via `lsof` (Unix) / `netstat` (Windows).
  See [KillVerb.cs](dotnet/src/Rig/Verbs/KillVerb.cs); pure PID parsers covered by tests.

### Semantic mismatch (probably leave as-is, but document)
- **`clean`** means different things: .NET `clean` = `dotnet clean` (light); Node `clean` =
  `rm -rf dist/build/.next/...` (heavier). In .NET the "nuke outputs" job lives in `rebuild`.
  Same verb, inverted weight — defensible per-ecosystem, but help text should make the
  difference explicit since users move between them.

### Ecosystem-idiom pairs (keep, but consider cross-aliases)
- **`run` (.NET) vs `dev` (Node)** and **`restore` (.NET) vs `install` (Node).** Intent-aliases
  already paper over this (`r` → run/dev). Consider adding `dev` as a hidden alias in .NET and
  `run`/`restore` in Node so muscle memory transfers both directions.

## 2. Menu consistency

Both share the `coverage · kill · maintenance ▸ · config ▸ · quit` spine and identical
`← back` rendering ([Menu.cs:101](dotnet/src/Rig/Menu.cs#L101)).

- **Submenu label casing.** .NET capitalizes (`Maintenance ▸`, `Config ▸`,
  [Menu.cs:53](dotnet/src/Rig/Menu.cs#L53)); Node lowercases (`maintenance ▸`). Node's
  lowercase matches the verb names — align .NET to lowercase.
- **Config submenu contents differ:**
  - .NET: `default · info · doctor · setup`
  - Node: `info · doctor · setup · default · init · update`
  - .NET's menu omits `init` and `update` (they exist as verbs but aren't reachable from the
    menu). Add them, and pick one order (Node leads with `info`, .NET with `default`).
- **Maintenance submenu contents differ:**
  - .NET: `rebuild · restore · clean · format · outdated`
  - Node: `install · outdated · clean · rebuild`
  - .NET buries `format` here; Node promotes `format` to the top level (it's a dev-loop verb).
    Decide whether `format` is top-level or maintenance, and unify ordering.
- **Custom commands/scripts in the menu.** Node has a `scripts ▸` submenu surfacing
  package.json scripts; .NET has custom `commands` in config but never surfaces them in the
  menu. Add a `commands ▸` (or `scripts ▸`) group to .NET.
- **`watch` discoverability.** .NET has a dedicated `watch ▸` submenu; Node only supports the
  `w`/`watch` prefix modifier with no menu entry. Either add the submenu to Node or drop it
  from .NET and document the prefix in both.
- **Hints.** Node attaches a hint to every menu row (`"stop dev servers"`, `"{pm} install"`);
  .NET only greys-out *unavailable* items with a reason. Adopt Node's per-row hints in .NET so
  the menus feel like the same product.

## 3. CLI / UX consistency

- **Unknown-verb message.** Node prints a friendly
  `unknown verb "x". Run \`rig\` for the menu or \`rig --help\`.`; .NET falls back to
  System.CommandLine's default. Mirror Node's message in .NET.
- **Help-text phrasing is mostly aligned** — `doctor` is deliberately parallel
  ("Flag environment problems (sdk, restore, layout)" vs "(node, pm, install state)"), `info`
  is near-identical. Keep this discipline; it's the strongest consistency signal. The
  `--all`/`--filter` workspace flags (Node-only, no .NET analog) are a fair ecosystem
  difference — just keep the flag *names* reserved so they never mean something else in .NET.
- **Config schema is well-unified**, with `node.*` reserved-but-unused — good. One stale spot:
  [ROADMAP.md](ROADMAP.md) lists `doctor ❌` for .NET, but
  [dotnet/src/Rig/Verbs/DoctorVerb.cs](dotnet/src/Rig/Verbs/DoctorVerb.cs) now exists
  (untracked). Update the matrix.

## Suggested priority order

1. Confirm/fix the Node `publish` gap.
2. Add `init` + `update` to .NET's config menu.
3. Unify submenu label casing + ordering.
4. Add `coverage --full` to Node.
5. Reconcile the `add` project-targeting convention.
