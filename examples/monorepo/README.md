# demo monorepo

A tiny, **no-install** pnpm workspace for exercising the Node rig by hand —
verb detection, monorepo cwd-awareness, the focused menu, and `--root`.

There are no `node_modules` here and you don't need to install anything: rig
reads `package.json` (scripts + declared deps) and config files, so use
`--dry-run`, the menu, and `rig info` to see what it *would* do. (Actually
running `rig dev` etc. would fail — the tools aren't installed.)

## The packages (each a different detection profile)

| Package | What it shows |
|---|---|
| `@demo/web` | full dev loop from **scripts**: dev · build · test · lint · format (+ a `storybook` script) |
| `@demo/api` | a partial loop: dev · test |
| `@demo/ui` | **detection with no scripts** — build/typecheck from `typescript` + `tsconfig.json`, lint from `eslint` + `eslint.config.js` |
| `@demo/tools` | the npm placeholder `test` is **skipped**; `seed` / `deploy` show up as scripts |

## Running it

Use whichever you have:

```sh
rig <verb>                       # if @jcamp/rig is installed globally
bun /path/to/rig/node/src/rignode.ts <verb>   # straight from source
```

(The repo's dev shim aliases `rig`/`rignode` to the source, so `rig` just works.)

## A guided tour

```sh
# 1. See what rig discovered for the whole workspace
rig info
#    → per-package detected verbs; note @demo/ui has verbs with no scripts,
#      and @demo/tools has none (its only "test" is the npm placeholder).

# 2. cwd-awareness — rig targets the package you're in, no picker
cd packages/web
rig build --dry-run              # → builds @demo/web
rig test --dry-run               # → tests @demo/web

# 3. detection-only package (@demo/ui has NO scripts)
cd ../ui
rig info                         # lists typecheck · lint · build — all detected, no scripts
rig typecheck                    # → "typescript is a dependency but isn't installed — run `pnpm install`"
                                 #   rig found the verb from the declared tool; it just isn't installed here.

# 4. reach another package explicitly, or the whole repo
rig test api --dry-run           # → tests @demo/api from anywhere
rig --root test --dry-run        # → ignores cwd; names a package (or run it from the root)

# 5. the interactive menus (need a TTY)
rig                              # in packages/web → focused on @demo/web,
                                 #   with "⌂ whole repo ▸" to zoom out
cd ../..
rig                              # at the root → whole-repo menu,
                                 #   with "focus a package ▸" to zoom in
```

Tip: `rig completion zsh` (or bash/pwsh) installs tab-completion; the candidates
it offers reflect exactly this per-package detection.
