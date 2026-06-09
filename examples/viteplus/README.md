# demo: Vite+ dispatch

A tiny, **no-install** repo showing rig's [Vite+](https://viteplus.dev) awareness:
in a Vite+ project rig stays the front-end but swaps the *engine* — `rig test`
runs `vp test`, `rig add` runs `vp add`, and so on — while dry-run, `.env`, the
`→` echo, and the menu all keep working. It's the same idea as rig's `.NET`
hand-off, one layer in: rig is the steering wheel, Vite+ is the motor.

## What makes this a Vite+ repo

Two things, both readable without installing anything:

- `package.json` declares **`vite-plus`** as a devDependency — rig's opt-in signal.
- `vite.config.ts` imports `defineConfig` from `vite-plus` (where a real project's
  Vite / Vitest / Oxlint / Oxfmt / task config lives).

Detection also needs the `vp` binary. A real repo has it (the VoidZero install or
`node_modules/.bin/vp`); here a **stub [`vp`](./vp)** stands in and just echoes
what it was handed, so the hand-off is visible with zero install. Point rig at it:

```sh
export RIG_VP_TOOL="$(pwd)/vp"   # only needed for this stub demo
```

## A guided tour

```sh
export RIG_VP_TOOL="$(pwd)/vp"

# 1. rig sees a Vite+ repo — the dev-loop verbs are hinted as `vp …`
rig info

# 2. the engine swap (note the verb *renames*, not just pass-through)
rig test --dry-run         # → vp test
rig build --dry-run        # → vp build
rig lint --dry-run         # → vp lint  (Oxlint)
rig format --dry-run       # → vp fmt   (rig `format` ↔ vp `fmt`, Oxfmt)

# 3. actually run one — the stub prints what real `vp` would receive
rig test                   # → vp test  /  [stub vp] test

# 4. dependency verbs map too (rig `uninstall` ↔ vp `remove`, `upgrade` ↔ `update`)
rig add lodash --dry-run   # → vp add lodash
rig uninstall lodash -n    # → vp remove lodash
rig dlx cowsay -n          # → vp dlx cowsay
```

**Convention-first still wins.** An explicit `package.json` script beats `vp`: add
`"scripts": { "test": "vitest run" }` here and `rig test` becomes `pnpm run test`
again — `vp` only fills the gap where you *haven't* written a script.

**Not everything maps.** `typecheck` stays native (`tsc --noEmit`): `vp check` also
lints and formats, so it isn't a pure type-check. `global`/`ci` stay native too — vp
routes them through the package manager identically. And `kill`/`cd`/`doctor`/
`coverage` have no `vp` analog. All fall straight through to rig's native behavior.

**Monorepos.** A project token becomes a `vp --filter` rather than being dropped —
`rig add lodash web` → `vp add lodash --filter web`.

## Turn it off

Delete the `vite-plus` line from `package.json` (or unset `RIG_VP_TOOL` so the
binary can't be found) and rig reverts to native dispatch — `rig test` becomes
`pnpm run test` again. Detection is all-or-nothing on those two signals.
