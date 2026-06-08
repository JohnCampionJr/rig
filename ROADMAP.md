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
| outdated | ✅ | ✅ |
| clean / rebuild | ✅ | ✅ |
| publish | ✅ | ✅ |
| kill (port/proc) | ✅ | ✅ (port-aware) |
| scripts → verbs (auto) | ➖ | ✅ |
| graph run (`--all`, dep order) | ❌ | ✅ |
| interactive menu | ✅ | ✅ (back-nav) |
| fuzzy matching | ✅ | ✅ |
| shell completion | ✅ (zsh/bash/pwsh) | ✅ (gunshi) |
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
| unit tests | ✅ | ✅ (66) |
| published | ✅ | 🔜 (ready) |

## Remaining work

- **Unify the config format across both tools** — one `.rig.json` filename, shared
  top-level keys, and tool-specific settings namespaced under `dotnet` / `node`
  (additive: the .NET tool still reads its legacy top-level keys). _In progress._
- **Publish `@jcamp/rig`** to npm (publish-ready).
</content>
</invoke>
