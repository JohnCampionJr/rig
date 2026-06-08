import { existsSync } from 'node:fs'
import { join, dirname } from 'node:path'
import type { PackageManager } from './types.js'

const LOCKFILES: Array<[string, PackageManager]> = [
  ['pnpm-lock.yaml', 'pnpm'],
  ['yarn.lock', 'yarn'],
  ['bun.lockb', 'bun'],
  ['bun.lock', 'bun'],
  ['package-lock.json', 'npm'],
  ['npm-shrinkwrap.json', 'npm'],
]

/**
 * Detect the package manager for a repo: the `packageManager` field wins
 * (corepack), otherwise the lockfile, otherwise npm.
 */
export function detectPackageManager(root: string, rootPkg?: Record<string, unknown>): PackageManager {
  const declared = typeof rootPkg?.packageManager === 'string' ? rootPkg.packageManager : ''
  for (const pm of ['pnpm', 'yarn', 'bun', 'npm'] as const) {
    if (declared.startsWith(`${pm}@`) || declared === pm) return pm
  }
  for (const [file, pm] of LOCKFILES) {
    if (existsSync(join(root, file))) return pm
  }
  return 'npm'
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
