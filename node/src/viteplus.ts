import { existsSync } from 'node:fs'
import { delimiter, join } from 'node:path'
import { findBin } from './pm.js'
import type { PackageInfo, Session, Workspace } from './types.js'

/**
 * Vite+ awareness — the mirror of `delegate.ts`'s .NET hand-off, one layer in.
 *
 * Where `delegate.ts` hands the *whole* process to the .NET rig when you're in a
 * .NET project, this keeps rig as the front-end and swaps only the *engine*: in a
 * Vite+ repo `rig test` runs `vp test`, `rig add` runs `vp add`, and so on — but
 * still through rig's own `run()`, so `--dry-run`, `.env`, the `→` echo, and the
 * menu all keep working. rig stays the steering wheel; Vite+ becomes one more
 * backend it dispatches to, exactly like npm/pnpm/dotnet.
 *
 * An explicit package.json script still wins (convention-first — see resolveDevLoop),
 * and verbs with no `vp` analog fall through to rig's native behavior.
 */

/**
 * rig verb → Vite+ (`vp`) subcommand. Absent verbs have no analog → rig native.
 *
 * Deliberately *not* mapped:
 *  - `typecheck`: `vp check` runs format **and** lint **and** type checks, so it's
 *    not a pure typecheck — rig keeps `typecheck` as native `tsc --noEmit`.
 *  - `global` / `ci`: vp routes these through the package manager identically to
 *    rig's native path (`npm i -g`, `… --frozen-lockfile`), so there's nothing to
 *    gain by dispatching — they stay native.
 */
export const VP_VERBS: Record<string, string> = {
  dev: 'dev',
  build: 'build',
  test: 'test',
  lint: 'lint', // → Oxlint
  format: 'fmt', // rig `format` ↔ `vp fmt` (Oxfmt)
  install: 'install',
  add: 'add',
  uninstall: 'remove', // rig `uninstall` ↔ `vp remove`
  upgrade: 'update', // rig `upgrade` ↔ `vp update`
  outdated: 'outdated',
  dlx: 'dlx',
}

/** The `vp` subcommand for a rig verb, or null when there's no analog. Pure. */
export function mapVerb(rigVerb: string): string | null {
  return VP_VERBS[rigVerb] ?? null
}

/**
 * Does the repo opt into Vite+? Signal: the `vite-plus` dep on the root package —
 * it's what supplies `defineConfig` for the unified `vite.config.ts`, so it's a
 * cheap, unambiguous marker (plain Vite projects don't carry it). Pure.
 */
export function hasViteplusDep(rootPackage: PackageInfo): boolean {
  const raw = rootPackage.raw
  for (const field of ['dependencies', 'devDependencies'] as const) {
    const deps = raw[field]
    if (deps && typeof deps === 'object' && 'vite-plus' in (deps as Record<string, unknown>)) return true
  }
  return false
}

/** Is `cmd` resolvable on $PATH? (covers the standalone global `vp` install). */
function onPath(cmd: string): boolean {
  const exts = process.platform === 'win32' ? ['.exe', '.cmd', '.bat', ''] : ['']
  for (const dir of (process.env.PATH ?? '').split(delimiter)) {
    if (!dir) continue
    for (const ext of exts) if (existsSync(join(dir, cmd + ext))) return true
  }
  return false
}

/**
 * Locate the `vp` binary: $RIG_VP_TOOL override, then a local
 * `node_modules/.bin/vp` (the `npm i -D vite-plus` path), then $PATH (the curl /
 * irm standalone install). Returns what to spawn, or null when vp isn't present.
 */
export function findVpTool(root: string): string | null {
  const override = process.env.RIG_VP_TOOL
  if (override) return existsSync(override) ? override : null
  return findBin('vp', root, root) ?? (onPath('vp') ? 'vp' : null)
}

/**
 * Resolve the usable `vp` tool for a workspace (dep present *and* binary found),
 * or null. Touches the filesystem / $PATH, so call it **once** per session — the
 * result is cached on `Session.viteplusTool` (see session.ts) and read back via
 * {@link viteplusCommand}.
 */
export function resolveViteplusTool(workspace: Workspace): string | null {
  return hasViteplusDep(workspace.rootPackage) ? findVpTool(workspace.root) : null
}

/**
 * Build the `vp` command for a rig verb given an already-resolved tool, or null
 * when there's no tool or the verb has no analog. Pure — the testable core.
 */
export function viteplusCommandWith(
  tool: string | null,
  rigVerb: string,
  extraArgs: string[] = [],
): { file: string; args: string[] } | null {
  if (!tool) return null
  const sub = mapVerb(rigVerb)
  return sub ? { file: tool, args: [sub, ...extraArgs] } : null
}

/** Session-level convenience: build the `vp` command using the cached tool. */
export function viteplusCommand(
  session: Session,
  rigVerb: string,
  extraArgs: string[] = [],
): { file: string; args: string[] } | null {
  return viteplusCommandWith(session.viteplusTool ?? null, rigVerb, extraArgs)
}
