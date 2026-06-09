# rig — cross-implementation consistency

Comparison of the .NET (`dotnet/`) and Node (`node/`) implementations, focused on
CLI surface, the interactive menu, and command semantics. Verified against source.

What's already aligned: both have `--dry-run/-n`, `--quiet/-q`, `--no-env`; the same
`→ / ✓ / ! / ✗` glyphs and cyan/green/yellow/red palette; a shared alias core
(`b t fmt k i def od rb c`); `← back` rendering in menus; and a unified config schema
(shared top-level keys, `dotnet.*` / `node.*` namespaces).

## 1. Command consistency

### Real gaps (parity matrix claims ✅ but isn't)
- ✅ ~~**`publish` doesn't exist in Node.** [ROADMAP.md](ROADMAP.md) claims parity, but there is
  no `publish` verb in [node/src/commands.ts](node/src/commands.ts). Either build it or
  correct the matrix.~~ Resolved by **correcting the matrix** — not a real gap. .NET's `publish`
  is a built-in because `dotnet publish` is canonical; Node has no canonical `publish` (the
  app-artifact equivalent is `build`), so any `publish` script in `package.json` is auto-surfaced
  as `rig publish` via the script→verb mechanism ([commands.ts:289-294](node/src/commands.ts#L289-L294)).
  A hardcoded Node verb would shadow that user script. Matrix now reads `➖ (via script→verb)`
  with a footnote.
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
- ✅ ~~**`coverage --full`** exists in the .NET CLI and in *both* config schemas (`coverage.full`),
  but Node has no `--full` flag — the config key is unreachable from Node's CLI. Add the flag.~~
  Resolved by **removing the dead key, not adding the flag** — `full` has no Node analog. In .NET,
  rig runs ReportGenerator itself and picks the HTML shape (`Html` multi-file vs
  `HtmlInline_AzurePipelines` single-file, [CoverageVerb.cs:79](dotnet/src/Rig/Verbs/CoverageVerb.cs#L79));
  in Node, rig delegates to vitest/jest, whose report shape lives in *their* config — rig can't
  control it. So `full` is now .NET-only: dropped from Node's [types.ts](node/src/types.ts) and
  [rig.schema.json](node/rig.schema.json). While here, fixed a related gap — Node's
  `coverage.open`/`coverage.min` config keys weren't read either (only the CLI flags were); they
  now fold in as defaults mirroring .NET's `ResolveOptions`
  ([coverage.ts](node/src/verbs/coverage.ts)). Node typecheck + 74 tests pass.
- ✅ ~~**`add` targets the project differently.** .NET: `add [package] --project/-p`.
  Node: `add <package> [project]` positional + `--dev/-D`. Pick one convention.~~
  Resolved by adopting **Node's positional style** (less typing): .NET is now
  `add <package> [project]` ([Commands.cs](dotnet/src/Rig/Verbs/Commands.cs),
  [AddVerb.cs](dotnet/src/Rig/Verbs/AddVerb.cs)). `--project/-p` is kept as a back-compat
  alias (wins if both are given), since the tool already shipped. `--dev/-D` stays Node-only —
  genuinely N/A for NuGet. Forwarding (`-- --version 1.2.3`), the `--project` alias, and the
  sole-project fallback are all verified by dry-run; 143 .NET tests still pass.
- ✅ ~~**`kill`** is port-aware in Node (`--port`, repeatable) but not in .NET.~~
  .NET `kill` now takes `--port N` (repeatable), and a bare numeric arg (`rig kill 3000`) is
  treated as a port, matching Node. Frees listeners via `lsof` (Unix) / `netstat` (Windows).
  See [KillVerb.cs](dotnet/src/Rig/Verbs/KillVerb.cs); pure PID parsers covered by tests.
- ✅ ~~**@antfu/ni dependency verbs were Node-only.**~~ Node grew `uninstall` (`remove`/`rm`),
  `global` (`g`), `dlx` (`x`), `upgrade`, and `ci` for ni parity (commands resolved through
  `package-manager-detector`). The three with clean `dotnet`-CLI analogues are now mirrored:
  `uninstall`/`remove`/`rm` → `dotnet remove <proj> package` (symmetric twin of `add`),
  `global`/`g` → `dotnet tool install --global`, and `dlx`/`x` → `dnx` (the .NET 10 one-off
  runner). See [DependencyVerbs.cs](dotnet/src/Rig/Verbs/DependencyVerbs.cs). **Kept Node-only**
  (no native `dotnet` verb, within reason): `upgrade` (`nu` — `dotnet` has no "bump all packages"
  command; `outdated` covers *listing*) and `ci` (`nci` — frozen restore needs `packages.lock.json`,
  uncommon in .NET; a `restore --locked` flag could be added later if wanted).

### Semantic mismatch (kept per-ecosystem, documented)
- ✅ ~~**`clean`** means different things: .NET `clean` = `dotnet clean` (light); Node `clean` =
  `rm -rf dist/build/.next/...` (heavier). In .NET the "nuke outputs" job lives in `rebuild`.~~
  Resolved as **defensible per-ecosystem** — each `clean` matches its ecosystem's standard
  expectation (MSBuild's incremental clean vs. removing JS build-output dirs). Left as-is, but the
  help text now makes the weight explicit so users moving between them aren't surprised:
  - .NET: `"dotnet clean the solution (MSBuild-aware, light; use rebuild to nuke bin/obj)"`
    ([Commands.cs:206](dotnet/src/Rig/Verbs/Commands.cs#L206)).
  - Node: `"Remove build-output dirs (dist/build/.next/… ; not node_modules)"`
    ([commands.ts:246](node/src/commands.ts#L246)).

### Ecosystem-idiom pairs (keep, but consider cross-aliases)
- ✅ ~~**`run` (.NET) vs `dev` (Node)** and **`restore` (.NET) vs `install` (Node).**~~
  Cross-aliases added both ways, symmetric for both pairs:
  - .NET: `run` gains alias `dev`; `restore` gains alias `install`
    ([Commands.cs](dotnet/src/Rig/Verbs/Commands.cs)). These are real System.CommandLine
    aliases, so they show in `--help` (matching how `r`/`res` already surface).
  - Node: `run → dev` and `restore → install` added to the `ALIASES` map
    ([commands.ts](node/src/commands.ts)), resolved in preparse so `--help` stays clean.
  - Caveat: in Node, aliases win over same-named package.json scripts — a (rare) script literally
    named `run` or `restore` would now be shadowed. Consistent with the existing alias design
    (`t`/`i`/`c` already shadow single-letter scripts).

## 2. Menu consistency — ✅ resolved

Both share the `coverage · kill · watch ▸ · maintenance ▸ · config ▸ · quit` spine and
identical `← back` rendering ([Menu.cs](dotnet/src/Rig/Menu.cs),
[menu.ts](node/src/menu.ts)).

- ✅ ~~**Submenu label casing.** .NET capitalizes (`Maintenance ▸`, `Config ▸`); Node
  lowercases.~~ .NET now lowercases every group label (`watch ▸`, `maintenance ▸`, `config ▸`,
  `commands ▸`) to match Node and the verb names.
- ✅ ~~**Config submenu contents differ** — .NET omits `init`/`self-update`.~~ Both now show
  `info · doctor · setup · default · init · self-update` in the same order.
- ✅ ~~**Maintenance contents + `format` placement.**~~ Decision: `format` is a dev-loop verb,
  so it's now **top-level in both** (.NET top: `run · build · test · coverage · format · kill ·
  publish`). Maintenance unifies to `[restore|install] · outdated · clean · rebuild` (the
  `restore`/`install` lead is the kept ecosystem idiom).
- ✅ ~~**Custom commands/scripts in the menu.** .NET never surfaced config `commands`.~~ .NET now
  shows a `commands ▸` group (only when the repo defines custom commands), mirroring Node's
  `scripts ▸`. The two stay ecosystem-shaped: Node surfaces package.json scripts, .NET surfaces
  `.rig.json` commands.
- ✅ ~~**`watch` discoverability.** Node had only the `w`/`watch` prefix.~~ Node now has a
  `watch ▸` submenu (pick dev/build/test → runs with `--watch`), mirroring .NET. The prefix and
  `-w` flag still work in both.
- ✅ ~~**Hints.** Node hints every row; .NET only greyed unavailable items.~~ .NET now attaches a
  per-row hint (`run a project`, `tests + coverage`, `clean + build`, …). One fidelity gap, not
  a parity one: .NET's hints are static action descriptions; Node's are the live resolved command.

## 3. CLI / UX consistency

- ✅ ~~**Unknown-verb message.** Node prints a friendly
  `unknown verb "x". Run \`rig\` for the menu or \`rig --help\`.`; .NET falls back to
  System.CommandLine's default.~~ Mirrored in .NET via a pre-parse guard in
  [Program.cs](dotnet/src/Rig/Program.cs): an unknown leading verb (not a known name/alias,
  not an option-like `-…` token) now prints the same message and exits 1 — replacing
  System.CommandLine's "Unrecognized command or argument" + full help dump (which also wrongly
  exited 0). Bad flags on a *real* verb still get the framework's normal parse error.
  (Glyph note: .NET's `Ui.Error` colors the line red with no `✗`, matching every other .NET
  error; Node's `ui.error` prepends `✗` — a pre-existing tool-wide styling difference left as-is.)
- ✅ **Help-text phrasing is mostly aligned** — verified still true: `doctor` is deliberately
  parallel ("Flag environment problems (sdk, restore, layout)" vs "(node, pm, install state)"),
  `info` is near-identical ("Show what rig discovered/resolved for this repo" vs "…discovered…").
  Keep this discipline; it's the strongest consistency signal. On the workspace flags, one
  correction to the original audit:
  - **`--all` / `-a`** is genuinely Node-only (run a verb across all workspace packages in dep
    order) and both forms are free in .NET — keep the name reserved. .NET has no analog by
    design: a solution already aggregates its projects, so a bare `rig test` covers them all.
  - **`--filter` is *not* Node-only — it already collides by ecosystem idiom.** Node `--filter
    <glob>` selects workspace *packages* (the pnpm/turbo idiom); .NET `test --filter <expr>` is
    the raw test-platform expression (the native `dotnet test --filter`,
    [Commands.cs:78](dotnet/src/Rig/Verbs/Commands.cs#L78)). Both are idiomatic and defensible
    per-ecosystem (like `clean`) — left as-is, not force-aligned. No code change needed: each
    tool's help text already disambiguates ("Raw test-platform filter expression" vs "limit
    --all to packages matching a glob or substring").
- ✅ ~~**Config schema is well-unified**, with `node.*` reserved-but-unused — good. One stale
  spot: [ROADMAP.md](ROADMAP.md) lists `doctor ❌` for .NET, but
  [dotnet/src/Rig/Verbs/DoctorVerb.cs](dotnet/src/Rig/Verbs/DoctorVerb.cs) now exists.~~
  Matrix is now accurate: `doctor | ✅ | ✅`, and ".NET doctor" is gone from "Remaining work"
  (the verb is wired in [Program.cs](dotnet/src/Rig/Program.cs#L78) and
  [Commands.cs:264](dotnet/src/Rig/Verbs/Commands.cs#L264)). Also refreshed the stale unit-test
  cell while there: `✅ (143)` / `✅ (74)` (was `(66)` for Node).

## Status — ✅ all audit items resolved

Every item above is closed. The outcomes split three ways:

- **Aligned in code** — `completion`, `--version -v`, port-aware `kill`, the `run`/`dev` &
  `restore`/`install` cross-aliases, the whole menu pass (casing, config/maintenance contents,
  `commands ▸`, `watch ▸`, hints), the friendly unknown-verb message, the `add` positional
  project convention, and folding Node's `coverage` config defaults.
- **Resolved by correcting the docs** — `publish` (Node has no canonical analog; matrix fixed),
  `coverage --full` (removed the dead Node key rather than adding a no-op flag), and the
  doctor/ROADMAP matrix staleness.
- **Kept as defensible per-ecosystem differences, now documented** — `clean`'s weight, and the
  `--filter` idiom collision (`dotnet test --filter` vs pnpm-style package selection).

No open follow-ups.
