# Changelog

All notable changes to `@jcamp/rig` (the Node port) are documented here.

## [Unreleased]

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
