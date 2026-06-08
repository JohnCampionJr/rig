# Changelog

All notable changes to `@jcamp/rig` (the Node port) are documented here.

## [Unreleased]

### Changed
- **Package-manager detection now uses `package-manager-detector`** (the library
  `@antfu/ni` uses) instead of a hand-rolled lockfile check. This picks up cases
  the old logic missed — notably `pnpm-workspace.yaml` with no lockfile → pnpm —
  plus the `packageManager` field, corepack, yarn berry, and walking up parents.

### Added
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
  menu (and, from there, a `<pkg> ▸` item to drop back into the current
  package). Switching re-renders in place. Single-package repos and the root are
  unchanged.

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
  now explains *why* instead of a terse "command not found"; a tool that's a
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
