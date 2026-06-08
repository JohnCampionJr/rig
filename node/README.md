# @jcamp/rig

A convention-first **Node** dev launcher. `rig` wraps the everyday package-manager
loop — dev, build, test, typecheck, lint, coverage, kill — with workspace discovery,
fuzzy matching, an interactive menu, shell completion, and per-repo settings. The
overriding goal is **less typing** and never having to remember where each project's
commands live.

> A Node port of the .NET [`rig`](https://github.com/JohnCampionJr/rig) tool — same
> ergonomics, Node-native verbs.

```sh
npm i -g @jcamp/rig      # or pnpm add -g @jcamp/rig

rig                      # interactive menu
rig dev                  # run the dev server (picks the package in a monorepo)
rig test                 # run tests
rig build --all          # build every workspace package in dependency order
```

## Why

Every repo has the same commands hiding behind different names and locations:
`npm run dev` here, `pnpm --filter web dev` there, `tsc --noEmit` for typecheck,
some `playground` script over there. `rig` discovers them and gives you **one
muscle-memory front-end**:

- **Menu-first.** Bare `rig` opens a picker — arrow keys (or Backspace to go back),
  each row shows the *real* command it will run, so you learn them over time.
- **Run from anywhere.** Discovery anchors at the workspace root; you never `cd`.
- **Convention over config.** A vanilla repo needs **zero** configuration; an optional
  `.rig.json` supplies only what can't be inferred.

## Verbs

Any **unambiguous prefix** of a verb resolves (`rig cov`, `rig typ`). In a monorepo a
trailing token picks the package (`rig dev api`, fuzzy). Args after `--` are forwarded.

| Verb | Alias | What |
|------|-------|------|
| `dev` / `run` | `d` / `r` | Run the `dev` script (fallback `start`); `-w` watch |
| `build` | `b` | `build` script, else detected `tsc`; `--all`/`--filter` for graph build |
| `test` | `t` | `test` script, else detected vitest/jest; `--all`/`--filter` |
| `typecheck` | `tc` | `typecheck` script, else `tsc --noEmit` |
| `lint` | `l` | `lint` script, else `eslint .` |
| `format` | `fmt` | `format` script, else `prettier --write .` |
| `coverage` | `c` | Tests + coverage (vitest/jest); `--open` the report, `--min` gates the line % |
| `‹any script›` | — | Every other `package.json` script becomes a verb (playground, demo, deploy…) |
| `kill` | `k` | Stop dev-server processes by package, or `--port <n>` |
| `install` | `inst` | Install dependencies (detected package manager) |
| `add <pkg> [project]` | — | Add a dependency to a package (`-D` for dev) |
| `outdated` | `od` | List dependencies with newer versions (recursive in a monorepo) |
| `clean` | — | Remove build outputs (`dist`, `.turbo`, `.next`, `.output`, …) |
| `rebuild` | `rb` | Clean build outputs, then build |
| `default [project]` | `def` | Show or set the default package |
| `info` | `i` | Show what rig discovered (pm, packages, scripts) |
| `doctor` | — | Flag environment problems (node version, pm, install state) |
| `init` | — | Scaffold a commented `.rig.json` |
| `setup` | — | Interactive walkthrough to set preferences |
| `update` | — | Update rig itself (`--check` to only report) |
| `complete <shell>` | — | Print a shell-completion script (zsh / bash / pwsh) |

**Global flags:** `--dry-run`/`-n` (print the command, run nothing), `--quiet`/`-q`,
`--no-env` (skip `.env`). **Watch** either way: `rig dev -w` or `rig watch dev` / `rig w d`.

Bare `rig` opens the menu: the everyday verbs up top (each showing its underlying
command), then `coverage`, `kill`, and grouped `▸ scripts · maintenance · config`
sub-menus. Backspace / Esc moves back a level; pick something and it runs once.

## Package managers & workspaces

rig **detects** the package manager from the lockfile (or the `packageManager` field)
and proxies to **npm / pnpm / yarn / bun**. Workspaces are first-class: a "project" is
a workspace package. `rig info` lists them; package-scoped verbs let you pick one (the
default is marked), and `rig build --all` runs across the graph in dependency order:

```sh
rig build --all                 # core → ui → app (topological)
rig test --all --filter web     # only packages matching "web"
```

## Configuration (`.rig.json`, all optional)

`rig init` scaffolds it; everything is inferred otherwise. A `$schema` reference gives
you autocomplete and validation in editors. Machine writes (`rig default`, `rig setup`)
splice values **in place, preserving your comments**.

`.rig.json` is shared with the .NET rig. Shared keys (`defaultProject`, `exclude`,
`quiet`, `env`, `envPresets`, `commands`, `aliases`, `kill`, `coverage`) live at the top
level; tool-specific settings live under a `dotnet` key (the .NET rig) or a `node` key
(the Node rig). The Node rig ignores the `dotnet` block; the `node` block is reserved for
future Node-specific settings.

```jsonc
{
  "$schema": "https://raw.githubusercontent.com/JohnCampionJr/rig/main/node/rig.schema.json",
  "defaultProject": "web",                         // when several packages are runnable
  "exclude": ["*-bench", "examples/*"],            // hide packages from pickers
  "env": { "FORCE_COLOR": "1" },                   // applied to every command
  "envPresets": { "log": { "DEBUG": "app:*" } },   // `rig test --log`
  "quiet": false
}
```

A global `~/.rig.json` (override with `$RIG_GLOBAL_CONFIG`) applies to every repo; the
repo config layers on top (repo wins per key, dictionaries union). `.env` / `.env.local`
load automatically (ambient env wins over the file; `.rig.json` env wins over both).

## Coverage

`rig coverage` runs your `coverage`/`test:coverage` script, or detects vitest/jest and
runs with `--coverage`. `--open` opens the generated HTML report; `--min <pct>` fails the
command when line coverage drops below the threshold (reads `coverage/coverage-summary.json`,
so enable a `json-summary` reporter).

## Shell completion

```sh
rig complete zsh  > ~/.zfunc/_rig      # then ensure ~/.zfunc is on $fpath
rig complete bash > ~/.local/share/bash-completion/completions/rig
```

`rig <tab>` then completes verbs and options.

## License

MIT © John Campion Jr
