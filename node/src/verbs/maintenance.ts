import { existsSync, lstatSync, realpathSync } from 'node:fs'
import { rm } from 'node:fs/promises'
import { isAbsolute, join, relative, resolve } from 'node:path'
import { addCmd, executeCmd, frozenCmd, globalAddCmd, installCmd, uninstallCmd, upgradeCmd } from '../pm.js'
import { viteplusCommand } from '../viteplus.js'
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

/**
 * In a Vite+ repo, dispatch a maintenance verb to `vp` (run at the root, through
 * rig). Returns the exit code, or null when this isn't a Vite+ repo / the verb has
 * no `vp` analog — in which case the caller runs its native package-manager path.
 */
async function tryViteplus(session: Session, rigVerb: string, extraArgs: string[] = []): Promise<number | null> {
  const vp = viteplusCommand(session, rigVerb, extraArgs)
  if (!vp) return null
  // Echo a clean `vp …` even though we spawn an absolute path (PATH / .bin / override).
  return run(vp.file, vp.args, {
    cwd: session.workspace.root,
    env: session.env,
    display: `vp ${vp.args.join(' ')}`,
  })
}

/** `rig install` — install dependencies via the detected package manager. */
export async function install(session: Session): Promise<number> {
  const vp = await tryViteplus(session, 'install')
  if (vp !== null) return vp
  const { file, args } = installCmd(session.workspace.pm)
  return run(file, args, { cwd: session.workspace.root, env: session.env })
}

/** `rig outdated` — list packages with newer versions (recursive in a monorepo). */
export async function outdated(session: Session): Promise<number> {
  const vp = await tryViteplus(session, 'outdated')
  if (vp !== null) return vp
  const { pm, root, isMonorepo } = session.workspace
  const args = ['outdated']
  if (isMonorepo && (pm === 'pnpm' || pm === 'yarn')) args.push('-r')
  return run(pm, args, { cwd: root, env: session.env })
}

/**
 * True when `target` resolves to a location strictly inside `root` — no `..`
 * escape, and not `root` itself. Pure (path-only); the caller resolves symlinks
 * first so an ancestor link can't smuggle the real path out of the tree.
 */
export function isWithinRoot(root: string, target: string): boolean {
  const rel = relative(resolve(root), resolve(target))
  return rel.length > 0 && !rel.startsWith('..') && !isAbsolute(rel)
}

/** `rig clean` — remove build-output directories (not node_modules). */
export async function clean(session: Session): Promise<number> {
  const targets = cleanCandidates(session.workspace.packages).filter((p) => existsSync(p))
  if (targets.length === 0) {
    ui.info('nothing to clean')
    return 0
  }
  // Data-loss guard: a recursive delete should never leave the workspace. A
  // hostile workspace layout (a package dir reaching outside via `..`, or an
  // allowlisted name that's a symlink) could otherwise point `rm -rf` anywhere.
  const realRoot = realpathSync(session.workspace.root)
  let cleaned = 0
  for (const target of targets) {
    const rel = target.slice(session.workspace.root.length + 1) || target
    let real: string
    try {
      if (lstatSync(target).isSymbolicLink()) {
        ui.warn(`skipping symlink: ${rel}`)
        continue
      }
      real = realpathSync(target)
    } catch {
      continue // vanished between the existsSync filter and here
    }
    if (!isWithinRoot(realRoot, real)) {
      ui.warn(`refusing to remove outside the workspace: ${target}`)
      continue
    }
    if (isDryRun()) {
      ui.info(`would remove ${rel}`)
      continue
    }
    ui.command(`rm -rf ${rel}`)
    await rm(target, { recursive: true, force: true })
    cleaned++
  }
  if (!isDryRun()) ui.success(`cleaned ${cleaned} dir(s)`)
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
    ui.error('usage: rig add <package> [project] [-D]')
    return 1
  }
  // `vp` is workspace-aware: a project token becomes a `--filter` (not dropped).
  const vp = await tryViteplus(session, 'add', [
    pkgName,
    ...(opts.dev ? ['-D'] : []),
    ...(opts.token ? ['--filter', opts.token] : []),
  ])
  if (vp !== null) return vp
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

/** `rig global <pkg>` — install a package globally (ni's `ni -g`). Not package-scoped. */
export async function global(session: Session, pkgName: string | undefined): Promise<number> {
  if (!pkgName) {
    ui.error('usage: rig global <package>')
    return 1
  }
  const { file, args } = globalAddCmd(session.workspace.agent, pkgName)
  return run(file, args, { cwd: session.workspace.root, env: session.env })
}

/** `rig uninstall <pkg> [project]` — remove a dependency from a package (ni's `nun`). */
export async function uninstall(
  session: Session,
  pkgName: string | undefined,
  opts: { token?: string } = {},
): Promise<number> {
  if (!pkgName) {
    ui.error('usage: rig uninstall <package> [project]')
    return 1
  }
  const vp = await tryViteplus(session, 'uninstall', [
    pkgName,
    ...(opts.token ? ['--filter', opts.token] : []),
  ])
  if (vp !== null) return vp
  const packages = session.workspace.packages
  const target = resolveTarget(session, packages, opts.token)
  let pkg: PackageInfo
  if (target.kind === 'none') {
    ui.error(target.reason)
    return 1
  } else if (target.kind === 'pkg') {
    pkg = target.pkg
  } else if (!isInteractive()) {
    ui.error('several packages match; name one (e.g. `rig uninstall <pkg> <project>`).')
    return 1
  } else {
    const picked = await pickPackage(target.packages, session.config.defaultProject, `Remove ${pkgName} from which package?`)
    if (!picked || picked === BACK) return 1
    pkg = picked
  }

  const cmd = uninstallCmd(session.workspace.agent, pkgName)
  if (!cmd) {
    ui.error(`${session.workspace.pm} has no uninstall command rig can run.`)
    return 1
  }
  ui.dim(`${pc.dim('package:')} ${pkg.name}`)
  return run(cmd.file, cmd.args, { cwd: pkg.dir, env: session.env })
}

/** `rig dlx <pkg> [args…]` — run a one-off package without installing (ni's `nlx`). */
export async function dlx(
  session: Session,
  pkgName: string | undefined,
  extraArgs: string[] = [],
): Promise<number> {
  if (!pkgName) {
    ui.error('usage: rig dlx <package> [args…]')
    return 1
  }
  const vp = await tryViteplus(session, 'dlx', [pkgName, ...extraArgs])
  if (vp !== null) return vp
  const cmd = executeCmd(session.workspace.agent, pkgName, extraArgs)
  if (!cmd) {
    ui.error(`${session.workspace.pm} cannot execute one-off packages.`)
    return 1
  }
  return run(cmd.file, cmd.args, { cwd: session.workspace.root, env: session.env })
}

/** `rig ci` — frozen / clean install from the lockfile (ni's `nci`). */
export async function ci(session: Session): Promise<number> {
  const cmd = frozenCmd(session.workspace.agent)
  if (!cmd) {
    ui.error(`${session.workspace.pm} has no frozen-install command.`)
    return 1
  }
  return run(cmd.file, cmd.args, { cwd: session.workspace.root, env: session.env })
}

/** `rig upgrade [pkg…] [-i]` — upgrade dependencies to newer versions (ni's `nu`). */
export async function upgrade(
  session: Session,
  pkgs: string[] = [],
  opts: { interactive?: boolean } = {},
): Promise<number> {
  const vp = await tryViteplus(session, 'upgrade', [...pkgs, ...(opts.interactive ? ['-i'] : [])])
  if (vp !== null) return vp
  const { agent, pm, root } = session.workspace
  let cmd = upgradeCmd(agent, pkgs, opts.interactive ?? false)
  if (!cmd && opts.interactive) {
    // npm has no interactive upgrade — fall back to a plain upgrade with a note.
    ui.warn(`${pm} has no interactive upgrade; running a plain upgrade instead.`)
    cmd = upgradeCmd(agent, pkgs, false)
  }
  if (!cmd) {
    ui.error(`${pm} has no upgrade command rig can run.`)
    return 1
  }
  return run(cmd.file, cmd.args, { cwd: root, env: session.env })
}
