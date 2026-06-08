# Changelog

All notable changes to **rig** are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project adheres
to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed
- Root resolution no longer climbs past a `.git` ancestor: a stray solution or
  config *outside* the repository (e.g. a `*.sln` up in the home directory) can no
  longer hijack the repo root when the repo's own solution sits in a subdirectory.

### Added
- `rig doctor` — environment health check: the .NET SDK (and whether it satisfies
  a `global.json` pin), restore state, the solution/project layout, and the test
  project. Exits non-zero only on an error-level finding, so it works as a CI /
  pre-push gate. Mirrors the Node `rig doctor`.

### Changed
- **Config layout unified with the Node `rig`.** Shared keys stay at the top level;
  .NET-only settings move under a `dotnet` namespace (`dotnet.solution`,
  `dotnet.test.project`, `dotnet.coverage.{settings,collector,license}`,
  `dotnet.rebuild`, `dotnet.publish`), and env presets move to a top-level
  `envPresets`. Fully additive — the previous flat layout still loads, with the
  namespaced value winning when both are present. `rig init` / `rig setup` now write
  the new shape; a `node` namespace is accepted and ignored.

### Added
- `rig update [--check]` — updates the rig tool itself to the latest published
  version (checks nuget.org for what's newer than the running build). `--check`
  only reports. On macOS/Linux it updates in place; on Windows — where the running
  `rig.exe` is locked — it hands off to a detached helper that waits for rig to
  exit, then updates in a new window.

## [1.1.0] - 2026-05-31

### Added
- `rig outdated` (`od`) — `dotnet list package --outdated` on the discovered
  solution, with `--vulnerable` / `--deprecated` lenses and `--transitive` /
  `--prerelease`. Restores first when a project hasn't been, so it works in one step.
- Interactive menu: picking `run`, `publish`, or `kill` now opens a project
  sub-menu (the configured default is marked) instead of silently acting — so the
  available projects are discoverable. `kill`'s sub-menu offers "all runnable
  projects" or a single target; `publish` no longer errors on ambiguity from the menu.
- `rig kill [project]` now takes an optional project argument to target one app,
  and with no argument sweeps **every** runnable project (not just the default) —
  the "stop everything I started" behavior.

### Changed
- The interactive menu is reorganized: the everyday loop (`run`, `build`, `test`,
  `coverage`, `kill`, `publish`) stays at the top level, and the long tail moves
  into grouped `▸` sub-menus — **Watch** (run/test/build), **Maintenance**
  (rebuild/restore/clean/format/outdated), and **Config** (default/info/setup) —
  trimming the top menu from ~16 rows to ~10.
- `rig kill` on Windows now matches the **full command line** via CIM
  (`Win32_Process.CommandLine`) and kills each match's process tree (`taskkill /T`),
  matching the Unix `pkill -f` behavior. Previously it matched only the image name
  (`taskkill /IM`), so the `dotnet run`/`dotnet watch` driver survived (and could
  respawn the app), and a framework-dependent run with no apphost was missed
  entirely. `kill.match` patterns are now command-line substrings on both platforms.

## [1.0.0] - 2026-05-31

First stable release. Rolls up everything since 0.1.2 (the unreleased 0.2.0 work).

### Added
- New everyday verbs: `restore` (`res`), `clean`, `format` (`fmt`) — `dotnet
  restore`/`clean`/`format` on the discovered solution.
- `rig add <package>` — `dotnet add package` that auto-resolves the target project
  (default → sole → prompt), so you skip naming it; `--project` to override, args
  after `--` forward (e.g. `--version`).
- `--configuration`/`-c` is now a real option on `build`, `rebuild`, and `test`
  (it was `run`-only); `publish` gains `-c`/`--rid`/`--output`/`--self-contained`/
  `--single-file` CLI overrides plus a `publish.configuration` config default
  (CLI > config > Release).
- Global `--dry-run`/`-n` — prints what each verb would run (or, for `rebuild`,
  the bin/obj dirs it would delete; for `kill`, the processes that would die)
  without doing it.

### Fixed
- `ConfigWriter` no longer overwrites an existing, non-empty `.rig.json` when an
  edit can't be spliced in place (e.g. a non-object parent) — it refuses and
  reports, instead of clobbering comments and other keys. (`rig setup` surfaces it.)
- An empty / whitespace-only / malformed `.rig.json` (or `~/.rig.json`) now
  degrades to defaults instead of crashing every command with a stack trace.
- Args after `--` are no longer mis-bound to a verb's optional positional:
  `rig run -- migrate` forwards `migrate` to the app instead of treating it as the
  project name. The `watch` modifier (`rig w run -- x`) is fixed by the same change.
- Custom-command (shell form) passthrough args are shell-quoted, so values
  containing spaces, quotes, `;`, `$`, or backticks stay literal (POSIX).
- `.env` double-quoted values keep an escaped `\"` instead of truncating at it.
- Nested-key writes into a single-line object (e.g. the `init` template's
  `"coverage": { … }`) insert inline instead of leaving a stray newline; a leading
  UTF-8 BOM is tolerated.

### Added
- `exclude` config — glob patterns (matched on a project's name or relative path)
  for projects rig should ignore, keeping demos/spikes/benchmarks out of the
  run/default/publish/kill pickers and the menu. Globs support `*` and `?`; the
  repo's and `~/.rig.json`'s lists union.
- `--quiet`/`-q` flag and `quiet` config — suppress the `→ command` echo
  (results, warnings, and errors still print). The flag overrides the config.
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

[Unreleased]: https://github.com/JohnCampionJr/rig/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/JohnCampionJr/rig/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/JohnCampionJr/rig/compare/v0.1.2...v1.0.0
[0.1.2]: https://github.com/JohnCampionJr/rig/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/JohnCampionJr/rig/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/JohnCampionJr/rig/releases/tag/v0.1.0
