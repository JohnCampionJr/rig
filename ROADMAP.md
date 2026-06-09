# rig — roadmap

Two tools, kept at feature parity: the mature .NET `rig` and the Node `@jcamp/rig`.

## Positioning

- **rig (.NET, [`dotnet/`](dotnet/))** — mature, full-featured, shipped (NuGet v1.1.0).
  The reference for .NET behavior.
- **@jcamp/rig (Node, [`node/`](node/))** — equally full-featured, 66 tests,
  publish-ready. The reference for Node behavior and most feature implementations.

## Feature parity matrix

Legend: ✅ yes · ❌ no · 🔜 planned · ➖ n/a

| Feature | rig (.NET) | @jcamp/rig (Node) |
| --- | --- | --- |
| Node ecosystem | ❌ | ✅ |
| .NET ecosystem | ✅ | ❌ |
| run / dev | ✅ | ✅ |
| build | ✅ | ✅ |
| test | ✅ | ✅ |
| typecheck | ➖ | ✅ |
| lint | ➖ | ✅ |
| format | ✅ | ✅ |
| coverage | ✅ (HTML) | ✅ (vitest/c8) |
| watch | ✅ | ✅ |
| install / restore | ✅ | ✅ |
| add package | ✅ | ✅ |
| uninstall / remove | ✅ | ✅ |
| global install (`ni -g`) | ✅ (`dotnet tool`) | ✅ |
| dlx / one-off run (`nlx`) | ✅ (`dnx`) | ✅ |
| upgrade (`nu`) | ➖ (no native verb) | ✅ |
| frozen install (`nci`) | ➖ (no native verb) | ✅ |
| outdated | ✅ | ✅ |
| clean / rebuild | ✅ | ✅ |
| publish | ✅ (`dotnet publish`) | ➖ (via script→verb) |
| kill (port/proc) | ✅ | ✅ (port-aware) |
| scripts → verbs (auto) | ➖ | ✅ |
| graph run (`--all`, dep order) | ❌ | ✅ |
| interactive menu | ✅ | ✅ (back-nav) |
| `cd` to a project/package | ✅ | ✅ |
| fuzzy matching | ✅ | ✅ |
| shell completion | ✅ (zsh/bash/pwsh) | ✅ (zsh/bash/pwsh) |
| cross-ecosystem completion | ✅ (shared `[suggest]`) | ✅ (shared `[suggest]`) |
| `--help` / `--version` | ✅ | ✅ |
| aliases · dry-run · quiet | ✅ | ✅ |
| config file (JSONC, comment-safe) | ✅ `.rig.json` | ✅ `.rig.json` (+ `$schema`) |
| global config | ✅ `~/.rig.json` | ✅ `~/.rig.json` |
| `.env` loading | ✅ | ✅ |
| env presets | ✅ | ✅ |
| default project | ✅ | ✅ |
| init / setup | ✅ | ✅ |
| doctor | ✅ | ✅ |
| self-update | ✅ | ✅ |
| install method | dotnet tool (NuGet) | npm |
| unit tests | ✅ (148) | ✅ (131) |
| published | ✅ | 🔜 (ready) |

**`publish`:** `.NET` has a built-in verb because `dotnet publish` is a canonical framework
command (RID / self-contained / single-file wired from `publish` config). Node has no canonical
`publish` — it could mean `npm publish` (registry), a deploy script, or nothing; the
app-deployment equivalent of `dotnet publish` is Node's `build`. So a `publish` script in any
`package.json` is automatically surfaced as `rig publish` via the script→verb mechanism. A
hardcoded Node `publish` verb would be presumptuous and would shadow that user script.

## Remaining work

- **Publish `@jcamp/rig`** to npm (publish-ready).

## Done

- **Unified config format across both tools** — one `.rig.json` filename, shared
  top-level keys, and tool-specific settings namespaced under `dotnet` / `node`
  (additive: the .NET tool still reads its legacy top-level keys). Both tools and
  both schema files (`rig.schema.json`, `node/rig.schema.json`) implement it.
