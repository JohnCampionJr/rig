import { existsSync } from 'node:fs'
import { join, dirname } from 'node:path'
import { detect, resolveCommand, type Agent, type AgentName, type ResolvedCommand } from 'package-manager-detector'
import type { PackageManager } from './types.js'

/**
 * Map a package-manager-detector agent to rig's supported set. Deno (and any
 * unknown / no result) falls back to npm — rig has no deno command path.
 */
export function toPackageManager(name: AgentName | undefined): PackageManager {
  return name === 'pnpm' || name === 'yarn' || name === 'bun' ? name : 'npm'
}

/** A detected package manager: the coarse `pm` family plus the precise `agent`. */
export interface PmDetection {
  pm: PackageManager
  /** The detector's agent string, preserving the yarn classic/Berry split. */
  agent: Agent
}

/**
 * Detect the package manager at or above `cwd`, following @antfu/ni's logic
 * (lockfiles, the `packageManager` field, `pnpm-workspace.yaml`, corepack, …)
 * via the shared `package-manager-detector` library — the same one ni uses.
 *
 * Returns both the coarse `pm` (used by most of rig) and the precise `agent`
 * (e.g. `yarn@berry`) which we hand to `resolveCommand` for exact ni parity.
 */
export async function detectPm(cwd: string): Promise<PmDetection> {
  const result = await detect({ cwd })
  const pm = toPackageManager(result?.name)
  // Keep `result.agent` only when the detected pm is one rig actually drives
  // (so deno/unknown collapse to plain `npm` for both fields).
  const agent: Agent = result && toPackageManager(result.name) === result.name ? result.agent : pm
  return { pm, agent }
}

/**
 * Build the argv to run a package.json script in a given directory.
 * Extra args are forwarded to the script.
 */
export function runScriptCmd(
  pm: PackageManager,
  script: string,
  args: string[] = [],
): { file: string; args: string[] } {
  switch (pm) {
    case 'npm':
      return { file: 'npm', args: ['run', script, ...(args.length ? ['--', ...args] : [])] }
    case 'pnpm':
      return { file: 'pnpm', args: ['run', script, ...args] }
    case 'yarn':
      return { file: 'yarn', args: ['run', script, ...args] }
    case 'bun':
      return { file: 'bun', args: ['run', script, ...args] }
  }
}

/** `pm install`. */
export function installCmd(pm: PackageManager): { file: string; args: string[] } {
  return { file: pm, args: ['install'] }
}

/** Add a dependency to the package rooted at `dir` (caller sets cwd). */
export function addCmd(
  pm: PackageManager,
  pkg: string,
  dev: boolean,
): { file: string; args: string[] } {
  switch (pm) {
    case 'npm':
      return { file: 'npm', args: ['install', dev ? '--save-dev' : '--save', pkg] }
    case 'pnpm':
      return { file: 'pnpm', args: ['add', ...(dev ? ['-D'] : []), pkg] }
    case 'yarn':
      return { file: 'yarn', args: ['add', ...(dev ? ['-D'] : []), pkg] }
    case 'bun':
      // `-D` (not `-d`) to match @antfu/ni — bun accepts both, ni uses `-D`.
      return { file: 'bun', args: ['add', ...(dev ? ['-D'] : []), pkg] }
  }
}

/** `pm outdated` with optional flags. */
export function outdatedCmd(pm: PackageManager): { file: string; args: string[] } {
  return { file: pm, args: ['outdated'] }
}

/** Shape a package-manager-detector ResolvedCommand into rig's {file, args}. */
function fromResolved(r: ResolvedCommand | null): { file: string; args: string[] } | null {
  return r ? { file: r.command, args: [...r.args] } : null
}

/**
 * The following four builders delegate to `package-manager-detector`'s
 * `resolveCommand` — the exact command table @antfu/ni resolves through — so
 * these commands are byte-identical to ni's, including the yarn classic/Berry
 * split (classic `execute` → `npx`, Berry → `yarn dlx`). They take an `agent`
 * (not the coarse `pm`) for that reason, and return null when a pm has no such
 * command (e.g. npm has no interactive upgrade).
 */

/** Run a one-off package without installing — ni's `nlx` (npx / pnpm dlx / yarn dlx|npx / bun x). */
export function executeCmd(agent: Agent, pkg: string, args: string[] = []): { file: string; args: string[] } | null {
  return fromResolved(resolveCommand(agent, 'execute', [pkg, ...args]))
}

/** Remove a dependency — ni's `nun` (npm uninstall / pnpm|yarn|bun remove). */
export function uninstallCmd(agent: Agent, pkg: string): { file: string; args: string[] } | null {
  return fromResolved(resolveCommand(agent, 'uninstall', [pkg]))
}

/** Frozen / clean install (CI) — ni's `nci` (npm ci / pnpm i --frozen-lockfile / …). */
export function frozenCmd(agent: Agent): { file: string; args: string[] } | null {
  return fromResolved(resolveCommand(agent, 'frozen', []))
}

/** Upgrade dependencies — ni's `nu` (`-i`/interactive when the pm supports it). */
export function upgradeCmd(
  agent: Agent,
  pkgs: string[] = [],
  interactive = false,
): { file: string; args: string[] } | null {
  return fromResolved(resolveCommand(agent, interactive ? 'upgrade-interactive' : 'upgrade', pkgs))
}

/**
 * Global install of a package — ni's `ni -g` (npm i -g / pnpm add -g / yarn
 * global add / bun add -g). Delegates to `resolveCommand` so it's agent-aware:
 * a yarn Berry global install routes through npm (`npm i -g`), exactly like ni.
 * Used by both `rig add -g` and `rig self-update`.
 */
export function globalAddCmd(agent: Agent, pkg: string): { file: string; args: string[] } {
  return fromResolved(resolveCommand(agent, 'global', [pkg])) ?? { file: 'npm', args: ['i', '-g', pkg] }
}

/**
 * Resolve a locally-installed CLI bin (node_modules/.bin/<name>) by walking up
 * from `fromDir` to `root`. Returns the absolute path, or null if absent.
 * This is how detect-as-fallback finds tsc/eslint/prettier/vitest.
 */
export function findBin(name: string, fromDir: string, root: string): string | null {
  const binName = process.platform === 'win32' ? `${name}.cmd` : name
  let dir = fromDir
  // Walk up until we pass the root (inclusive).
  for (;;) {
    const candidate = join(dir, 'node_modules', '.bin', binName)
    if (existsSync(candidate)) return candidate
    if (dir === root) break
    const parent = dirname(dir)
    if (parent === dir) break
    dir = parent
  }
  // Final check at root in case fromDir started below it.
  const atRoot = join(root, 'node_modules', '.bin', binName)
  return existsSync(atRoot) ? atRoot : null
}
