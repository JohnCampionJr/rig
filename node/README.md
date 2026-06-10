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
| `typecheck` | `tc` / `check` | `typecheck` script, else `tsc --noEmit` (`check` aliases here unless you have a `check` script) |
| `lint` | `l` | `lint` script, else `eslint .` |
| `format` | `fmt` | `format` script, else `prettier --write .` |
| `coverage` | `c` | Tests + coverage (vitest/jest); `--open` the report, `--min` gates the line % |
| `‹any script›` | — | Every other `package.json` script becomes a verb (playground, demo, deploy…) |
| `kill` | `k` | Stop dev-server processes by package, or `--port <n>` |
| `install` | `inst` | Install dependencies (detected package manager) |
| `ci` | — | Frozen / clean install from the lockfile (ni's `nci`) |
| `add <pkg> [project]` | — | Add a dependency to a package (`-D` for dev) |
| `global <pkg>` | `g` | Install a package globally (ni's `ni -g`) |
| `uninstall <pkg> [project]` | `remove` / `rm` | Remove a dependency from a package (ni's `nun`) |
| `upgrade [pkg…]` | — | Upgrade dependencies; `-i` interactive where supported (ni's `nu`) |
| `dlx <pkg> [args…]` | `x` | Run a one-off package without installing (ni's `nlx`) |
| `outdated` | `od` | List dependencies with newer versions (recursive in a monorepo) |
| `clean` | — | Remove build outputs (`dist`, `.turbo`, `.next`, `.output`, …) |
| `rebuild` | `rb` | Clean build outputs, then build |
| `default [project]` | `def` | Show or set the default package |
| `info` | `i` | Show what rig discovered (pm, packages, scripts) |
| `cd [query]` | — | Jump to a package dir (fuzzy match, or a picker); needs the `rig` shell wrapper from `rig completion` |
| `doctor` | — | Flag environment problems (node version, pm, install state) |
| `init` | — | Scaffold a commented `.rig.json` |
| `setup` | — | Interactive walkthrough to set preferences |
| `self-update` | — | Update rig itself (`--check` to only report) |
| `completion <shell>` | — | Print shell-completion setup (zsh / bash / pwsh); shared `[suggest]` protocol, cross-ecosystem |

**Global flags:** `--dry-run`/`-n` (print the command, run nothing), `--quiet`/`-q`,
`--no-env` (skip `.env`). **Watch** either way: `rig dev -w` or `rig watch dev` / `rig w d`.

Bare `rig` opens the menu: the everyday verbs up top (each showing its underlying
command), then `coverage`, `kill`, and grouped `▸ scripts · maintenance · config`
sub-menus. Backspace / Esc moves back a level; pick something and it runs once.

## Package managers & workspaces

rig **detects** the package manager from the lockfile (or the `packageManager` field)
and proxies to **npm / pnpm / yarn / bun** — via the same
[`package-manager-detector`](https://github.com/antfu-collective/package-manager-detector)
library [`@antfu/ni`](https://github.com/antfu-collective/ni) uses. Workspaces are
first-class: a "project" is a workspace package. `rig info` lists them; package-scoped
verbs let you pick one (the default is marked), and `rig build --all` runs across the
graph in dependency order:

```sh
rig build --all                 # core → ui → app (topological)
rig test --all --filter web     # only packages matching "web"
```

### Parity with [`@antfu/ni`](https://github.com/antfu-collective/ni)

If `ni`/`nr`/`nu` are in your fingers, the dependency verbs already are too. rig
resolves these through the same `package-manager-detector` command table ni does, so
the delegated verbs (`ci` / `upgrade` / `uninstall` / `dlx` / `global`) emit the
**byte-identical** command ni would across npm, pnpm, yarn (classic *and* Berry), and
bun — a parity suite ([`test/ni-parity.test.ts`](test/ni-parity.test.ts)) keeps them honest.

| ni | does | rig | command (pnpm e.g.) |
|----|------|-----|---------------------|
| `ni` | install all deps | `rig install` (`inst`) | `pnpm install` |
| `ni <pkg>` | add a dependency | `rig add <pkg> [project]` | `pnpm add <pkg>` |
| `ni <pkg> -D` | add a devDependency | `rig add <pkg> -D` | `pnpm add -D <pkg>` |
| `ni -g <pkg>` | global add | `rig global <pkg>` (`g`) | `pnpm add -g <pkg>` |
| `nci` | frozen / clean install | `rig ci` | `pnpm i --frozen-lockfile` |
| `nr <script> [args]` | run a script | `rig <script>` / `rig dev` / `rig run` | `pnpm run <script> [args]` |
| `nr` (no arg) | pick a script | bare `rig` (menu) | — |
| `nu` | upgrade deps | `rig upgrade [pkg…]` | `pnpm update [pkg…]` |
| `nu -i` | interactive upgrade | `rig upgrade -i` | `pnpm update -i` |
| `nun <pkg>` | uninstall a dep | `rig uninstall <pkg>` (`remove` / `rm`) | `pnpm remove <pkg>` |
| `nlx <pkg> [args]` | run a one-off package | `rig dlx <pkg> [args]` (`x`) | `pnpm dlx <pkg> [args]` |
| `na <args>` | raw agent passthrough | — | — |

rig is a launcher, not a drop-in ni: it has no `na` passthrough or `nr -` rerun, and it
adds a whole dev-loop layer (`build` / `test` / `typecheck` / `lint` / `format` /
`coverage` / `kill` / …) plus `outdated` (which *lists* newer versions — `upgrade`
actually bumps them) on top. Many thanks to **[antfu](https://github.com/antfu)** and the
`@antfu/ni` / `package-manager-detector` projects, which rig builds directly on.

## [Vite+](https://viteplus.dev) awareness

In a Vite+ repo — a `vite-plus` dependency plus a resolvable `vp` binary — rig stays the
front-end but swaps the **engine**: `rig test`/`build`/`lint` → `vp test`/`build`/`lint`,
`rig format` → `vp fmt`, and `rig add`/`uninstall`/`upgrade`/`outdated`/`dlx` → the matching
`vp` command — all still through rig's own runner, so `--dry-run`, `.env`, the `→` echo, and
the menu keep working. It's the same idea as the cross-ecosystem `.NET` hand-off, one layer
in: rig is the steering wheel, Vite+ is the motor.

- **Convention-first still wins** — an explicit `package.json` script beats `vp` dispatch.
- **In a monorepo** a project token becomes a `vp --filter` (`rig add lodash web` →
  `vp add lodash --filter web`), not dropped.
- **Kept native** (no gain from dispatch): `typecheck` (`vp check` also lints+formats, so it
  isn't a pure type-check), and `global` / `ci` (vp routes these through the package manager
  identically). Verbs with no `vp` analog fall through too.

Detection is all-or-nothing on those two signals (override the binary with `$RIG_VP_TOOL`),
resolved once per session. See the [`examples/viteplus`](../examples/viteplus) walkthrough.

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

Add one line for your shell, then restart it:

```sh
# zsh — ~/.zshrc
eval "$(rig completion zsh)"
# bash — ~/.bashrc
eval "$(rig completion bash)"
```
```powershell
# pwsh — $PROFILE
Invoke-Expression (& rig completion pwsh | Out-String)
```

`rig <tab>` then completes verbs, aliases, and workspace package names. The
generated script calls rig's shared `[suggest]` protocol — the *same* one the
.NET rig uses — so a single completer works in both ecosystems: in a .NET
project the request is forwarded to the .NET tool, in a Node project it's
answered here, whichever `rig` wins on `PATH`.

## Local development (`rigdev`)

To exercise your working tree as a real command without publishing or shadowing
the installed `rig`, build a standalone dev binary named `rigdev` (requires
[Bun](https://bun.sh)):

```sh
cd node
bun run install:rigdev          # compiles src/cli.ts → ~/.local/bin/rigdev
# or pick the target dir:
RIG_DEV_BIN=/some/dir/on/PATH bun run install:rigdev
```

`rigdev` is delegate-aware just like `rig` (it hands off to the .NET tool in a
.NET project). It's a snapshot of the source, so re-run `install:rigdev` after
editing to refresh it (the compile is ~150ms). For one-off runs without
installing, `bun src/cli.ts <args>` works too.

## License

MIT © John Campion Jr
