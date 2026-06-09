# Changelog

## 1.4.0

### Minor Changes

- 2039a28: Add @antfu/ni-parity dependency verbs and a parity test suite.

  New verbs map onto ni's: `uninstall` (`remove`/`rm`, ni's `nun`), `dlx` (`x`,
  ni's `nlx`), `ci` (frozen/clean install, ni's `nci`), `upgrade` (`-i`
  interactive, ni's `nu`), and `global` (`g`, global install, ni's `ni -g`). These
  resolve through the same `package-manager-detector` command table ni uses, so
  they emit byte-identical commands across npm / pnpm / yarn (classic _and_
  Berry) / bun.

  Fixes three command divergences from ni: `dlx` on classic yarn now uses `npx`
  (Berry uses `yarn dlx`), `dlx` no longer injects an implicit `npx -y`, and
  `add -D` on bun uses `-D` instead of `-d`. A new `test/ni-parity.test.ts`
  asserts rig's generated commands match ni's and pins the few intentional
  divergences.

  Also renames the self-update verb `update` → `self-update` so it no longer
  collides with the new `upgrade` dependency verb (renamed in the .NET rig too,
  keeping the two tools in sync).

- 2039a28: Add [Vite+](https://viteplus.dev) awareness: in a Vite+ repo (a `vite-plus`
  dependency + the `vp` binary), rig dispatches verbs to `vp` while staying the
  front-end — `rig test`/`build`/`lint` → `vp test`/`build`/`lint`,
  `rig format` → `vp fmt`, `rig add`/`uninstall`/`upgrade`/`outdated`/`dlx` →
  the matching `vp` command — all still flowing through rig's `run()`, so
  `--dry-run`, `.env`, the `→` echo, and the menu keep working. It's the same
  idea as the existing `.NET` hand-off, one layer in: rig is the steering wheel,
  Vite+ is the engine.

  Convention-first still wins: an explicit `package.json` script beats `vp`
  dispatch. In a monorepo a project token becomes a `vp --filter` (e.g.
  `rig add lodash web` → `vp add lodash --filter web`) rather than being dropped.

  Deliberately **not** dispatched: `typecheck` (`vp check` also lints+formats, so
  it's not a pure type-check — stays `tsc`), and `global`/`ci` (vp routes these
  through the package manager identically to rig's native path). Verbs with no `vp`
  analog (`kill`, `cd`, `doctor`, `coverage`, …) fall through to native behavior.

  Detection is all-or-nothing on the `vite-plus` dep + a resolvable `vp` (override
  with `$RIG_VP_TOOL`), resolved once per session. See the new `examples/viteplus`
  walkthrough.

### Patch Changes

- c8087e7: Fix and sync the config JSON schemas. `node/rig.schema.json` now mirrors the
  authoritative root `rig.schema.json` (rich `dotnet.*` namespace, deprecated-key
  annotations) so the two can't drift, and a stray `</content>` tag that left the
  root `rig.schema.json` as invalid JSON has been removed.

## [1.3.0] - 2026-06-08

### Changed

- **Inside a package, rig won't silently act on another.** When cwd is in a
  member package that can't run a verb, rig no longer falls through to a
  different package's sole candidate — it picks (interactive) or errors with
  guidance (`@demo/ui has no format; name a package …`). The sole-candidate
  convenience still applies from the workspace root / under `--root`.
- **Package-manager detection now uses `package-manager-detector`** (the library
  `@antfu/ni` uses) instead of a hand-rolled lockfile check. This picks up cases
  the old logic missed — notably `pnpm-workspace.yaml` with no lockfile → pnpm —
  plus the `packageManager` field, corepack, yarn berry, and walking up parents.

### Added

- **`rig cd [query]`** — jump to a package directory. Matching is path-aware
  (name, short name, relative path, or directory basename) and forgiving
  (exact → prefix → substring → subsequence, e.g. `aw` → `apps/web`); a name
  match outranks a path-only one. A query that matches nothing falls back to the
  picker; no query opens it straight away. Since a subprocess can't change the
  parent shell's directory, `rig completion`'s shell script now also installs a
  thin `rig` wrapper that does the `cd` (the command prints the dir to stdout,
  its menu to stderr). One `eval "$(rig completion zsh)"` enables both completion
  and `rig cd`; the package-argument completion now also offers short names.
- **Monorepo cwd-awareness.** Running rig inside a member package now targets
  that package by default — `rig test` in `packages/web` tests web with no
  picker (the current package beats the configured default; an explicit token
  like `rig test api` still wins). Pickers mark and pre-select the current
  package, and `rig info` shows it. The full repo stays reachable — verbs aren't
  scoped away, so root-level orchestration and cross-package commands are
  unchanged.
- **Focused menu.** Inside a member package, the bare-`rig` menu opens scoped to
  that package — its verbs run on it directly (no picker), it lists its own
  scripts — with a `⌂ whole repo ▸` item to switch up to the whole-workspace
  menu. From the whole-repo menu, `focus a package ▸` opens a picker (cwd's
  package pre-selected) to scope into any package. Switching re-renders in place.
  Single-package repos and the root are unchanged.
- **`--root` flag.** Act on the whole workspace from anywhere — `rig --root test`
  (or `rig test --root`) ignores the package cwd is in, and `rig --root` opens
  the whole-repo menu. The CLI counterpart to the menu's `⌂ whole repo`.

### Changed

- **Deterministic verb detection.** Which dev-loop verbs exist is now read from
  `package.json` instead of blindly probing `node_modules/.bin`. A verb shows
  (in the menu, completion, `--help`, and as a runnable command) only when it
  makes sense: a matching script, or the tool declared as a dependency (or
  installed). `tsc`/`eslint`/`prettier` fallbacks also require their config file
  (`tsconfig`/eslint/prettier config) to be present. `rig lint` no longer exists
  in a repo that has no lint script and no eslint.
- The npm `npm init` placeholder `test` script (`echo "Error: no test
specified" && exit 1`) is recognized and ignored, so `test` only appears with
  a real test setup. `test` also detects `jest` in addition to `vitest`.
- Running a known-but-inapplicable verb (e.g. `rig lint` where nothing lints)
  now explains _why_ instead of a terse "command not found"; a tool that's a
  declared dependency but not installed says to run install rather than failing
  obscurely.

## [1.2.0] - 2026-06-07

First published release. The version starts at 1.2.0 to track the sibling .NET
[`rig`](https://github.com/JohnCampionJr/rig) tool in lockstep.

### Added — initial Node port

- **Discovery**: package-manager detection (npm / pnpm / yarn / bun) from lockfile or
  the `packageManager` field; workspace + package + script discovery; run-from-root.
- **Dev-loop verbs** (scripts-first, detect-as-fallback): `dev`, `build`, `test`,
  `typecheck`, `lint`, `format` — run the matching `package.json` script, else invoke
  the detected tool (tsc / eslint / prettier / vitest).
- **Discovered scripts**: every other `package.json` script becomes a runnable verb.
- **coverage**: vitest/jest detection, `--open` the report, `--min` line-% gate.
- **Workspace graph run**: `--all` runs a verb across packages in dependency order;
  `--filter <glob|substring>` scopes it.
- **Maintenance**: `install`, `add`, `outdated`, `clean`, `rebuild`.
- **kill**: stop dev servers by package pattern or `--port`.
- **Config**: `info`, `doctor`, `init`, `setup`, `default`, `update`.
- **Interactive menu**: project-driven, shows each item's real command, single-key
  navigation with Backspace/Esc to go back; `watch ▸ · scripts ▸ · maintenance ▸ ·
config ▸`. The `watch ▸` group runs dev/build/test under `--watch`.
- **Config file**: single `.rig.json` with a `$schema`, JSONC-tolerant reads, and
  comment-preserving writes; global `~/.rig.json`; `.env` / `.env.local` loading;
  named `envPresets` as `--<preset>` flags. Shared with the .NET rig: shared keys live
  at the top level, while tool-specific settings are namespaced under `dotnet` (the .NET
  rig) and `node` (the Node rig); the Node rig ignores the `dotnet` block.
- **CLI**: built on [gunshi](https://gunshi.dev) — verb aliases, unambiguous-prefix
  expansion, the `watch` modifier, `--dry-run`/`--quiet`/`--no-env`, and auto `--help`.
- **Shell completion** (`rig completion zsh|bash|pwsh`) via a shared `[suggest]`
  protocol with the .NET rig — one generated completer works across both
  ecosystems (a .NET dir forwards to the .NET tool; a Node dir is answered here).
- **Cross-ecosystem delegation**: in a .NET project, `rig` hands off to the .NET
  rig, so a single `rig` works in either ecosystem. Ships three bins: `rig`
  (ecosystem-aware), `rignode` (force Node), `rigdotnet` (force .NET). Set
  `RIG_NO_DELEGATE=1` to force native behavior.
