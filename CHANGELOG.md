# Changelog

All notable changes to **rig** are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project adheres
to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- `rig setup` — an interactive walkthrough that shows what rig auto-detects, then
  lets you set the few things it can't infer (default project, ReportGenerator Pro
  license, coverage prefs) into either the repo's `.rig.json` or your user-wide
  `~/.rig.json`, comment-preserving. The license prompt is masked and steers you
  toward the global file so it stays out of source control.
- Persistent coverage defaults: `coverage.open` (auto-open the report),
  `coverage.full` (full multi-file report), and `coverage.min` (default line gate).
  The matching CLI flag always overrides the config default. `rig info` now has a
  `coverage defaults` row so a repo that silently gates at N% isn't a surprise.
- `JsoncEditor`/`ConfigWriter` now write nested keys (e.g. `coverage.license`) and
  typed values (string/bool/number) to either config file, still preserving
  comments, formatting, and key order. (Also fixes a stale `$schema` URL written
  into brand-new config files.)
- `rig coverage --min <pct>` fails with a non-zero exit when line coverage is
  below the threshold — useful in CI and as a local pre-push gate.
- `rig info` now flags unknown/typo'd top-level `.rig.json` keys (with a "did you
  mean …?" suggestion); System.Text.Json otherwise ignores them silently.
- `rig init` scaffolds a ready-to-fill `coverage.license` field (blank = the free
  engine), since the ReportGenerator Pro key is the one setting that isn't
  auto-discoverable.
- User-wide config: `~/.rig.json` (or `$RIG_GLOBAL_CONFIG`) is loaded under every
  repo's `.rig.json` — repo wins per key, `env`/`aliases`/`commands` union. Blank
  strings count as unset, so a repo's scaffolded `coverage.license: ""` falls
  through to a real key set once globally (never committed). `rig info` shows it.
- `rig info` now tags each config-sourced setting with its provenance —
  `(local)`, `(global)`, or `(local+global)` — and adds a `coverage license` row
  (masked: shows `set (Pro)` + source, never the key). Markers appear only when a
  global config is present.

- `rig run` and `rig test` accept `--framework`/`-f` (multi-TFM projects); `rig
  run` also accepts `--launch-profile` (a `launchSettings.json` profile). Both
  slot in before the `--` forwarding boundary.

### Changed
- Completion no longer suggests the help-option noise aliases (`-?`, `-h`, `/?`,
  `/h`) — `-h`/`--help` still work, they're just hidden from suggestions. Useful
  short flags (e.g. `-c`, `-w`) and `--help`/`--version` are kept.

## [0.1.2]

### Added
- `rig completion` with no shell prints per-shell setup instructions, so it's
  clear what to add and where.

### Changed
- zsh completion self-initializes the completion system (guarded `compinit`), so
  setup is a single line — `eval "$(rig completion zsh)"` — even on a bare zsh.
- Completion output documents the one-line install rather than implying you paste
  the whole script.

### Fixed
- Completion scripts are emitted on raw stdout (not via the styled console), so
  terminal-width wrapping no longer corrupts the script when captured with
  `eval "$(rig completion <shell>)"`.

## [0.1.1]

### Changed
- Shell completion is now **self-contained**: `rig completion <zsh|bash|pwsh>`
  emits a script that calls rig's built-in `[suggest]` directive directly.
  Removed the `dotnet-suggest` dependency — its apphost fails to start on Apple
  Silicon (hardened-runtime signature → CoreCLR `0x80070008`) and the broker
  route wasn't OS-agnostic.

## [0.1.0]

- Initial release — a convention-first .NET dev launcher: `run`, `build`,
  `rebuild`, `test`, `coverage`, `kill`, `publish`, `default`, `info`, `init`,
  and per-repo custom commands; zero-config discovery with an optional
  `.rig.json`; `.env` support; curated verb aliases and prefix matching; an
  interactive menu; and in-process coverage HTML via bundled ReportGenerator.

[Unreleased]: https://github.com/JohnCampionJr/rig/compare/v0.1.2...HEAD
[0.1.2]: https://github.com/JohnCampionJr/rig/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/JohnCampionJr/rig/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/JohnCampionJr/rig/releases/tag/v0.1.0
