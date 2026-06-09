---
'@jcamp/rig': minor
---

Add [Vite+](https://viteplus.dev) awareness: in a Vite+ repo (a `vite-plus`
dependency + the `vp` binary), rig dispatches verbs to `vp` while staying the
front-end — `rig test`/`build`/`lint` → `vp test`/`build`/`lint`,
`rig format` → `vp fmt`, `rig add`/`uninstall`/`upgrade`/`outdated`/`dlx` →
the matching `vp` command — all still flowing through rig's `run()`, so
`--dry-run`, `.env`, the `→` echo, and the menu keep working. It's the same
idea as the existing `.NET` hand-off, one layer in: rig is the steering wheel,
Vite+ is the engine.

Convention-first still wins: an explicit `package.json` script beats `vp`
dispatch. In a monorepo a project token becomes a `vp --filter` (e.g.
`rig add lodash web` → `vp add lodash --filter web`) rather than being dropped.

Deliberately **not** dispatched: `typecheck` (`vp check` also lints+formats, so
it's not a pure type-check — stays `tsc`), and `global`/`ci` (vp routes these
through the package manager identically to rig's native path). Verbs with no `vp`
analog (`kill`, `cd`, `doctor`, `coverage`, …) fall through to native behavior.

Detection is all-or-nothing on the `vite-plus` dep + a resolvable `vp` (override
with `$RIG_VP_TOOL`), resolved once per session. See the new `examples/viteplus`
walkthrough.
