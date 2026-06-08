import { existsSync, readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'

const RIG_CONFIG_NAMES = ['rig.config.json', '.rig.json']
const LOCKFILES = [
  'pnpm-lock.yaml',
  'yarn.lock',
  'bun.lockb',
  'bun.lock',
  'package-lock.json',
  'npm-shrinkwrap.json',
]

function hasWorkspaceMarker(dir: string): boolean {
  if (existsSync(join(dir, 'pnpm-workspace.yaml'))) return true
  const pkgPath = join(dir, 'package.json')
  if (!existsSync(pkgPath)) return false
  try {
    const pkg = JSON.parse(readFileSync(pkgPath, 'utf8')) as Record<string, unknown>
    return pkg.workspaces != null
  } catch {
    return false
  }
}

/**
 * Resolve the repo root by walking up from `start`. Precedence (first found
 * wins, each evaluated as we climb): rig.config.* → workspace marker → lockfile
 * → .git → the starting directory.
 */
export function resolveRoot(start: string = process.cwd()): string {
  let rigConfig: string | null = null
  let workspace: string | null = null
  let lock: string | null = null
  let git: string | null = null

  let dir = start
  for (;;) {
    if (!rigConfig && RIG_CONFIG_NAMES.some((n) => existsSync(join(dir, n)))) rigConfig = dir
    if (!workspace && hasWorkspaceMarker(dir)) workspace = dir
    if (!lock && LOCKFILES.some((n) => existsSync(join(dir, n)))) lock = dir
    if (!git && existsSync(join(dir, '.git'))) git = dir

    const parent = dirname(dir)
    if (parent === dir) break
    dir = parent
  }

  return rigConfig ?? workspace ?? lock ?? git ?? start
}
