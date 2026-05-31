# rig

A convention-first .NET dev launcher, packaged as a dotnet tool. `rig` wraps the
everyday `dotnet` loop — run, build, test, coverage, kill, publish — with project
discovery, fuzzy matching, an interactive menu, shell completion, and per-repo
custom commands. The overriding goal is **less typing**.

```sh
dotnet tool install --global rig
rig                 # interactive menu
rig test Archive    # run a test class (fuzzy match)
rig coverage --open # coverage + HTML report, opened
```

## Why

The valuable, reusable part of a hand-rolled dev launcher is the *ergonomics*;
the repo-specific lore (project names, test paths, build scripts) should be data,
not code. `rig` is convention-first: in a vanilla single-app + single-test repo it
needs **zero configuration**, and an optional `.rig.json` supplies only what can't
be inferred.

## Verbs

| Verb | Alias | What |
|------|-------|------|
| `run [project]` | `r` | Run a runnable project (`-w` watch, `-c` configuration; args after `--` go to the app) |
| `build` | `b` | Build the solution (`-w` watch) |
| `rebuild` | `rb` | Delete in-tree bin/obj (scoped to solution projects), then build (`--dry-run`) |
| `test [name]` | `t` | Run tests; bare name → fuzzy class match; `~ = !~ !=` filter shorthand; `--log`, `-w` |
| `coverage [name]` | `c` | Tests + coverage; in-process HTML report; `--full`, `--open` |
| `kill` | `k` | Terminate the app/test processes |
| `publish [project]` | `pub` | Self-contained `dotnet publish` |
| `default [project]` | `def` | Show or set the default run project (no run) |
| `info` | `i` | Show what rig discovered/resolved for this repo |
| `init` | — | Scaffold a commented `.rig.json` |
| `completion <shell>` | `comp` | Print shell-completion setup (dotnet-suggest) |

Bare `rig` opens an interactive menu. Any **unambiguous prefix** of a verb also
resolves (`rig cove`, `rig reb`). **Watch** run/test/build either way: the option
(`rig test -w`) or the leading modifier (`rig watch run`, `rig w r`).

## Configuration (`.rig.json`, all optional)

Auto-discovered (so they stay out of config): the solution, the test project, the
coverage runsettings, the coverage collector, the `kill` target, and the `rebuild`
targets. What's left is only what can't be inferred:

```jsonc
{
  "$schema": "https://raw.githubusercontent.com/JohnCampionJr/rig/main/rig.schema.json",
  "defaultProject": "MyApp",                                    // when several are runnable
  "test": { "envPresets": { "log": { "MYAPP_LOG": "1" } } },    // `rig test --log`
  "commands": { "deploy": "./deploy.sh" },                      // custom verbs (npm-scripts style)
  "aliases": { "coverage": "cov" }                              // override a verb's short alias
}
```

`rig init` scaffolds this; `rig run --remember` / `rig default <p>` write
`defaultProject` for you (comment-preserving). `.env` / `.env.local` are loaded
automatically (dotenv precedence).

## Coverage

Coverage renders in-process via the bundled **ReportGenerator** — no separate
install. Single-file inline report by default, `--full` for the multi-file report,
`--open` to open it; the headline `line %·branch %` prints to the console.
ReportGenerator **Pro** features unlock via the `REPORTGENERATOR_LICENSE` env var
(or `.rig.json` `coverage.license`) — `rig` bundles only the free Apache-2.0 engine.

## Completion

`rig` participates in `dotnet-suggest`. Install it once
(`dotnet tool install --global dotnet-suggest`) and add the snippet from
`rig completion <zsh|bash|pwsh>` to your shell profile — then Tab completes verbs
*and* discovered project/test names.

## Building from source

```sh
dotnet test Rig.slnx     # or: dotnet test tests/Rig.Tests/Rig.Tests.csproj
```

See [docs/DESIGN.md](docs/DESIGN.md) for architecture and the rationale behind the
key decisions.
