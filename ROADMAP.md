# rig — roadmap

Two tools, kept at feature parity: the mature .NET `rig` and the Node `rignode`.

## Positioning

- **rig (.NET, [`dotnet/`](dotnet/))** — mature, full-featured, shipped (NuGet v1.1.0).
  The reference for .NET behavior.
- **rignode (Node, [`node/`](node/))** — equally full-featured, 66 tests,
  publish-ready. The reference for Node behavior and most feature implementations.

## Feature parity matrix

Legend: ✅ yes · ❌ no · 🔜 planned · ➖ n/a

| Feature | rig (.NET) | rignode (Node) |
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
| outdated | ✅ | ✅ |
| clean / rebuild | ✅ | ✅ |
| publish | ✅ (`dotnet publish`) | ➖ (via script→verb) |
| kill (port/proc) | ✅ | ✅ (port-aware) |
| scripts → verbs (auto) | ➖ | ✅ |
| graph run (`--all`, dep order) | ❌ | ✅ |
| interactive menu | ✅ | ✅ (back-nav) |
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
| unit tests | ✅ (143) | ✅ (74) |
| published | ✅ | 🔜 (ready) |

**`publish`:** `.NET` has a built-in verb because `dotnet publish` is a canonical framework
command (RID / self-contained / single-file wired from `publish` config). Node has no canonical
`publish` — it could mean `npm publish` (registry), a deploy script, or nothing; the
app-deployment equivalent of `dotnet publish` is Node's `build`. So a `publish` script in any
`package.json` is automatically surfaced as `rig publish` via the script→verb mechanism. A
hardcoded Node `publish` verb would be presumptuous and would shadow that user script.

## Remaining work

- **Unify the config format across both tools** — one `.rig.json` filename, shared
  top-level keys, and tool-specific settings namespaced under `dotnet` / `node`
  (additive: the .NET tool still reads its legacy top-level keys). _In progress._
- **Publish `rignode`** to npm (publish-ready).
</content>
</invoke>
