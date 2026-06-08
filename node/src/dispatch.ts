import { run } from './exec.js'
import { filterPackages, topoSort } from './graph.js'
import { BACK, isInteractive, pickPackage } from './prompts.js'
import {
  candidatePackages,
  getDevLoopVerb,
  resolveDevLoop,
  resolveScript,
  type DevLoopVerb,
  type ResolvedCommand,
} from './resolve.js'
import { resolveTarget } from './target.js'
import { pc, ui } from './ui.js'
import type { PackageInfo, Session } from './types.js'

/** Execute a resolved command with the session's env overlay plus any extra env. */
export function runCommand(
  session: Session,
  command: ResolvedCommand,
  extraEnv?: Record<string, string>,
): Promise<number> {
  const env = extraEnv ? { ...session.env, ...extraEnv } : session.env
  return run(command.file, command.args, {
    cwd: command.cwd,
    env,
    display: command.display,
  })
}

export interface DispatchOptions {
  token?: string
  extraArgs?: string[]
  watch?: boolean
  /** Extra env overlay (e.g. from an env preset). */
  env?: Record<string, string>
}

/** Resolve and run a dev-loop verb for an already-chosen package (no prompting). */
export function runDevLoopForPackage(
  session: Session,
  verb: DevLoopVerb,
  pkg: PackageInfo,
  opts: DispatchOptions = {},
): Promise<number> {
  const resolution = resolveDevLoop(session, verb, pkg, opts.extraArgs ?? [])
  if (!resolution.ok) {
    ui.error(resolution.reason)
    return Promise.resolve(1)
  }
  const command = resolution.command
  if (opts.watch) {
    if (command.source === 'tool') {
      command.args = [...command.args, '--watch']
      command.display = `${command.display} --watch`
    } else {
      ui.warn(`watch applies to detected tools; add --watch inside the "${verb.name}" script instead.`)
    }
  }
  return runCommand(session, command, opts.env)
}

/** Resolve and run a discovered script for an already-chosen package (no prompting). */
export function runScriptForPackage(
  session: Session,
  scriptName: string,
  pkg: PackageInfo,
  opts: DispatchOptions = {},
): Promise<number> {
  const resolution = resolveScript(session, scriptName, pkg, opts.extraArgs ?? [])
  if (!resolution.ok) {
    ui.error(resolution.reason)
    return Promise.resolve(1)
  }
  return runCommand(session, resolution.command, opts.env)
}

/**
 * Resolve which package to act on for the CLI path, prompting when ambiguous.
 * Returns null (after printing guidance) when it can't be resolved. Exported so
 * package-scoped verbs (coverage, add) share one resolution policy.
 */
export async function pickTargetPackage(
  session: Session,
  candidates: PackageInfo[],
  action: string,
  token: string | undefined,
): Promise<PackageInfo | null> {
  const target = resolveTarget(session, candidates, token)
  if (target.kind === 'none') {
    ui.error(target.reason)
    return null
  }
  if (target.kind === 'pkg') return target.pkg
  if (!isInteractive()) {
    ui.error(
      token
        ? `"${token}" matches several packages; be more specific.`
        : `several packages can ${action}; name one (e.g. \`rig ${action} <pkg>\`).`,
    )
    return null
  }
  const picked = await pickPackage(target.packages, session.config.defaultProject, `Which package to ${action}?`, {
    current: session.currentPackage,
  })
  return picked && picked !== BACK ? picked : null
}

/** CLI entry for a dev-loop verb (dev/build/test/typecheck/lint/format). */
export async function dispatchDevLoop(
  session: Session,
  verbName: string,
  opts: DispatchOptions = {},
): Promise<number> {
  const verb = getDevLoopVerb(verbName)
  if (!verb) {
    ui.error(`unknown verb: ${verbName}`)
    return 1
  }
  const pkg = await pickTargetPackage(session, candidatePackages(session, verb), verbName, opts.token)
  if (!pkg) return 1
  return runDevLoopForPackage(session, verb, pkg, opts)
}

/**
 * Run a dev-loop verb across all candidate packages in dependency order,
 * stopping at the first failure. `--filter` narrows the set by glob.
 */
export async function dispatchDevLoopAll(
  session: Session,
  verbName: string,
  opts: { filter?: string; extraArgs?: string[]; env?: Record<string, string> } = {},
): Promise<number> {
  const verb = getDevLoopVerb(verbName)
  if (!verb) {
    ui.error(`unknown verb: ${verbName}`)
    return 1
  }
  let candidates = candidatePackages(session, verb)
  if (opts.filter) candidates = filterPackages(candidates, opts.filter)
  if (candidates.length === 0) {
    ui.error(opts.filter ? `no packages match "${opts.filter}" for ${verbName}` : `no packages can ${verbName}`)
    return 1
  }

  const ordered = topoSort(candidates)
  for (const pkg of ordered) {
    ui.info(pc.bold(pc.cyan(`\n• ${pkg.name}`)))
    const code = await runDevLoopForPackage(session, verb, pkg, { extraArgs: opts.extraArgs, env: opts.env })
    if (code !== 0) {
      ui.error(`${verbName} failed in ${pkg.name}`)
      return code
    }
  }
  ui.success(`${verbName} · ${ordered.length} packages`)
  return 0
}

/** CLI entry for an arbitrary discovered package.json script. */
export async function dispatchScript(
  session: Session,
  scriptName: string,
  opts: DispatchOptions = {},
): Promise<number> {
  const candidates = session.workspace.packages.filter((p) => p.scripts[scriptName] != null)
  if (candidates.length === 0) {
    ui.error(`no package defines a "${scriptName}" script.`)
    return 1
  }
  const pkg = await pickTargetPackage(session, candidates, scriptName, opts.token)
  if (!pkg) return 1
  return runScriptForPackage(session, scriptName, pkg, opts)
}
