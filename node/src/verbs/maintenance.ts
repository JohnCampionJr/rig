import { existsSync } from 'node:fs'
import { rm } from 'node:fs/promises'
import { join } from 'node:path'
import { addCmd, installCmd } from '../pm.js'
import { isDryRun, run } from '../exec.js'
import { resolveTarget } from '../target.js'
import { BACK, isInteractive, pickPackage } from '../prompts.js'
import { dispatchDevLoop } from '../dispatch.js'
import { pc, ui } from '../ui.js'
import type { PackageInfo, Session } from '../types.js'

/** Build-output directory names removed by `clean`/`rebuild` (never node_modules). */
export const CLEAN_DIRS = ['dist', 'build', '.output', '.nuxt', '.next', '.turbo', 'coverage']

/** Candidate clean paths across all packages (pure; existence filtered by the verb). */
export function cleanCandidates(packages: PackageInfo[]): string[] {
  const out: string[] = []
  for (const pkg of packages) {
    for (const dir of CLEAN_DIRS) out.push(join(pkg.dir, dir))
  }
  return out
}

/** `rig install` — install dependencies via the detected package manager. */
export async function install(session: Session): Promise<number> {
  const { file, args } = installCmd(session.workspace.pm)
  return run(file, args, { cwd: session.workspace.root, env: session.env })
}

/** `rig outdated` — list packages with newer versions (recursive in a monorepo). */
export async function outdated(session: Session): Promise<number> {
  const { pm, root, isMonorepo } = session.workspace
  const args = ['outdated']
  if (isMonorepo && (pm === 'pnpm' || pm === 'yarn')) args.push('-r')
  return run(pm, args, { cwd: root, env: session.env })
}

/** `rig clean` — remove build-output directories (not node_modules). */
export async function clean(session: Session): Promise<number> {
  const targets = cleanCandidates(session.workspace.packages).filter((p) => existsSync(p))
  if (targets.length === 0) {
    ui.info('nothing to clean')
    return 0
  }
  for (const target of targets) {
    const rel = target.slice(session.workspace.root.length + 1) || target
    if (isDryRun()) {
      ui.info(`would remove ${rel}`)
      continue
    }
    ui.command(`rm -rf ${rel}`)
    await rm(target, { recursive: true, force: true })
  }
  if (!isDryRun()) ui.success(`cleaned ${targets.length} dir(s)`)
  return 0
}

/** `rig rebuild` — clean build outputs, then build the target package. */
export async function rebuild(session: Session, token?: string): Promise<number> {
  const cleanCode = await clean(session)
  if (cleanCode !== 0) return cleanCode
  if (isDryRun()) ui.dim('then: build')
  return dispatchDevLoop(session, 'build', { token })
}

/** `rig add <pkg> [project]` — add a dependency to a package. */
export async function add(
  session: Session,
  pkgName: string | undefined,
  opts: { dev?: boolean; token?: string } = {},
): Promise<number> {
  if (!pkgName) {
    ui.error('usage: rig add <package> [project] [--dev]')
    return 1
  }
  const packages = session.workspace.packages
  const target = resolveTarget(session, packages, opts.token)
  let pkg: PackageInfo
  if (target.kind === 'none') {
    ui.error(target.reason)
    return 1
  } else if (target.kind === 'pkg') {
    pkg = target.pkg
  } else if (!isInteractive()) {
    ui.error('several packages match; name one (e.g. `rig add <pkg> <project>`).')
    return 1
  } else {
    const picked = await pickPackage(target.packages, session.config.defaultProject, `Add ${pkgName} to which package?`)
    if (!picked || picked === BACK) return 1
    pkg = picked
  }

  const { file, args } = addCmd(session.workspace.pm, pkgName, opts.dev ?? false)
  ui.dim(`${pc.dim('package:')} ${pkg.name}`)
  return run(file, args, { cwd: pkg.dir, env: session.env })
}
