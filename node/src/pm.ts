import { existsSync } from 'node:fs'
import { join, dirname } from 'node:path'
import { detect, type AgentName } from 'package-manager-detector'
import type { PackageManager } from './types.js'

/**
 * Map a package-manager-detector agent to rig's supported set. Deno (and any
 * unknown / no result) falls back to npm — rig has no deno command path.
 */
export function toPackageManager(name: AgentName | undefined): PackageManager {
  return name === 'pnpm' || name === 'yarn' || name === 'bun' ? name : 'npm'
}

/**
 * Detect the package manager at or above `cwd`, following @antfu/ni's logic
 * (lockfiles, the `packageManager` field, `pnpm-workspace.yaml`, corepack, …)
 * via the shared `package-manager-detector` library — the same one ni uses.
 */
export async function detectPm(cwd: string): Promise<PackageManager> {
  const result = await detect({ cwd })
  return toPackageManager(result?.name)
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
      return { file: 'bun', args: ['add', ...(dev ? ['-d'] : []), pkg] }
  }
}

/** `pm outdated` with optional flags. */
export function outdatedCmd(pm: PackageManager): { file: string; args: string[] } {
  return { file: pm, args: ['outdated'] }
}

/** Run a one-off package without installing (npx / pnpm dlx / yarn dlx / bunx). */
export function dlxCmd(
  pm: PackageManager,
  pkg: string,
  args: string[] = [],
): { file: string; args: string[] } {
  switch (pm) {
    case 'npm':
      return { file: 'npx', args: ['-y', pkg, ...args] }
    case 'pnpm':
      return { file: 'pnpm', args: ['dlx', pkg, ...args] }
    case 'yarn':
      return { file: 'yarn', args: ['dlx', pkg, ...args] }
    case 'bun':
      return { file: 'bunx', args: [pkg, ...args] }
  }
}

/** Update a global package via the PM (for `rig update`). */
export function globalAddCmd(
  pm: PackageManager,
  pkg: string,
): { file: string; args: string[] } {
  switch (pm) {
    case 'npm':
      return { file: 'npm', args: ['install', '--global', pkg] }
    case 'pnpm':
      return { file: 'pnpm', args: ['add', '--global', pkg] }
    case 'yarn':
      return { file: 'yarn', args: ['global', 'add', pkg] }
    case 'bun':
      return { file: 'bun', args: ['add', '--global', pkg] }
  }
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
