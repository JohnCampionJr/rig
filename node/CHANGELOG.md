# Changelog

All notable changes to `@jcamp/rig` (the Node port) are documented here.

## [Unreleased]

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
  navigation with Backspace/Esc to go back; `scripts ▸ · maintenance ▸ · config ▸`.
- **Config file**: single `rig.config.json` with a `$schema`, JSONC-tolerant reads, and
  comment-preserving writes; global `~/.rig.json`; `.env` / `.env.local` loading;
  named `envPresets` as `--<preset>` flags.
- **CLI**: built on [gunshi](https://gunshi.dev) — verb aliases, unambiguous-prefix
  expansion, the `watch` modifier, `--dry-run`/`--quiet`/`--no-env`, auto `--help`, and
  shell completion (`rig complete zsh|bash|pwsh`).
