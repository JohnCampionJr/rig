# Security

rig is a local developer CLI: it discovers your project's commands and runs them.
This note explains its trust boundaries so you know what's safe and what isn't.

## Trust model

**Treat a repository you run `rig` in as you'd treat its `package.json` scripts or
MSBuild targets — as executable code.** Specifically:

- **`.rig.json` is executable config.** Custom `commands` in string form run through
  a shell (`sh -c` / `cmd /c`) so pipes and `&&` work, exactly like an npm script.
  A hostile `.rig.json` can therefore run arbitrary code **when you invoke that
  custom verb**. Don't run `rig <custom-command>` in a repo you don't trust.
- **`env` / `.env` values flow into the commands rig spawns** (this is the point of
  them). `.env` files are parsed **literally** — there is no `$(...)` / backtick
  expansion — so a cloned `.env` cannot by itself execute code.

What rig deliberately does **not** do:

- It never executes anything from config just by being launched, by opening the
  interactive menu, or by tab-completion. Custom commands require explicit
  invocation.
- Built-in verbs spawn their tools with an explicit argument vector (no shell), so
  package names, script names, and forwarded args can't inject a second command.
  (On Windows, `.cmd`/`.bat` shims are invoked through `cmd.exe` with fully quoted,
  caret-escaped arguments rather than `{ shell: true }`.)

## Destructive operations

`rig clean` / `rig rebuild` remove build output recursively. They only target a
fixed allow-list of build directories (`dist`, `build`, `.next`, `.turbo`, … /
`bin`, `obj`) under discovered packages/projects, and each target is checked to be
**inside the workspace and not a symlink/junction** before deletion — so a stray or
hostile path can't redirect the delete elsewhere. Use `--dry-run` to preview.

## Updates

`rig self-update` checks the package registries over **HTTPS** (npmjs.org /
nuget.org) and runs your package manager / `dotnet tool` to install. It resolves
the sibling tool from `PATH` (or `RIG_NODE_TOOL` / `RIG_DOTNET_TOOL`); as with any
tool, a hostile `PATH` entry is outside rig's trust boundary.

## Reporting a vulnerability

Please report security issues privately via
[GitHub Security Advisories](https://github.com/JohnCampionJr/rig/security/advisories/new)
rather than a public issue. We'll acknowledge and work a fix with you.
