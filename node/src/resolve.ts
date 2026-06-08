import { existsSync } from 'node:fs'
import { basename, join } from 'node:path'
import { findBin, runScriptCmd } from './pm.js'
import { displayOf } from './exec.js'
import type { PackageInfo, Session } from './types.js'

/** A tool fallback: the bin to look for and the args to invoke it with. */
interface ToolFallback {
  bin: string
  args: string[]
  /** Extra gate beyond the bin existing (e.g. a tsconfig.json must be present). */
  requires?: (pkg: PackageInfo) => boolean
}

/** Definition of a known dev-loop verb. */
export interface DevLoopVerb {
  name: string
  /** Candidate script names, in priority order. */
  scripts: string[]
  /** Tool to detect & invoke when no script matches. */
  fallback?: ToolFallback
}

function hasTsconfig(pkg: PackageInfo): boolean {
  return existsSync(join(pkg.dir, 'tsconfig.json'))
}

/**
 * The known dev-loop verbs (scripts-first, detect-as-fallback). Order is the
 * user's working frequency — it drives the menu and `info` listing order.
 */
export const DEV_LOOP_VERBS: DevLoopVerb[] = [
  { name: 'dev', scripts: ['dev', 'start'] },
  {
    name: 'typecheck',
    scripts: ['typecheck', 'type-check'],
    fallback: { bin: 'tsc', args: ['--noEmit'], requires: hasTsconfig },
  },
  { name: 'lint', scripts: ['lint'], fallback: { bin: 'eslint', args: ['.'] } },
  { name: 'build', scripts: ['build'], fallback: { bin: 'tsc', args: [], requires: hasTsconfig } },
  { name: 'test', scripts: ['test'], fallback: { bin: 'vitest', args: ['run'] } },
  { name: 'format', scripts: ['format', 'fmt'], fallback: { bin: 'prettier', args: ['--write', '.'] } },
]

export function getDevLoopVerb(name: string): DevLoopVerb | undefined {
  return DEV_LOOP_VERBS.find((v) => v.name === name)
}

/** The first candidate script the package actually defines, or null. */
export function resolveScriptName(pkg: PackageInfo, candidates: string[]): string | null {
  for (const name of candidates) {
    if (pkg.scripts[name] != null) return name
  }
  return null
}

/** Annotate a command's echo with the package it runs in (unless the root). */
function withWhere(display: string, pkg: PackageInfo): string {
  return pkg.isRoot ? display : `${display}  (${pkg.relDir})`
}

/** A command resolved for execution. */
export interface ResolvedCommand {
  file: string
  args: string[]
  cwd: string
  /** What gets echoed before running. */
  display: string
  /** How the command was resolved (for diagnostics). */
  source: 'script' | 'tool'
}

export type Resolution =
  | { ok: true; command: ResolvedCommand }
  | { ok: false; reason: string }

/**
 * Resolve a dev-loop verb for a package: run the matching package.json script
 * if present, else detect & invoke the canonical tool. `extraArgs` are
 * forwarded to the script/tool.
 */
export function resolveDevLoop(
  session: Session,
  verb: DevLoopVerb,
  pkg: PackageInfo,
  extraArgs: string[] = [],
): Resolution {
  const scriptName = resolveScriptName(pkg, verb.scripts)
  if (scriptName) {
    const { file, args } = runScriptCmd(session.workspace.pm, scriptName, extraArgs)
    return {
      ok: true,
      command: { file, args, cwd: pkg.dir, display: withWhere(displayOf(file, args), pkg), source: 'script' },
    }
  }

  const fb = verb.fallback
  if (fb && (!fb.requires || fb.requires(pkg))) {
    const binPath = findBin(fb.bin, pkg.dir, session.workspace.root)
    if (binPath) {
      const args = [...fb.args, ...extraArgs]
      return {
        ok: true,
        command: {
          file: binPath,
          args,
          cwd: pkg.dir,
          display: withWhere(displayOf(fb.bin, args), pkg),
          source: 'tool',
        },
      }
    }
  }

  const scriptList = verb.scripts.map((s) => `"${s}"`).join(' or ')
  const toolNote = fb ? `, and ${fb.bin} is not installed` : ''
  return {
    ok: false,
    reason: `${pkg.name} has no ${scriptList} script${toolNote}.`,
  }
}

/** True when the verb can run for this package (script present or tool available). */
export function canRunVerb(session: Session, verb: DevLoopVerb, pkg: PackageInfo): boolean {
  if (resolveScriptName(pkg, verb.scripts)) return true
  const fb = verb.fallback
  if (!fb) return false
  if (fb.requires && !fb.requires(pkg)) return false
  return findBin(fb.bin, pkg.dir, session.workspace.root) != null
}

/** Packages a verb can run against (for pickers and capability gating). */
export function candidatePackages(session: Session, verb: DevLoopVerb): PackageInfo[] {
  return session.workspace.packages.filter((p) => canRunVerb(session, verb, p))
}

/**
 * A short, human-facing description of what a dev-loop verb will do for a
 * package — the *underlying* command, so menus teach the real tool.
 * e.g. "vite" (script body) or "tsc --noEmit" (tool fallback).
 */
export function describeDevLoop(session: Session, verb: DevLoopVerb, pkg: PackageInfo): string | null {
  const scriptName = resolveScriptName(pkg, verb.scripts)
  if (scriptName) return pkg.scripts[scriptName] ?? scriptName
  const fb = verb.fallback
  if (fb && (!fb.requires || fb.requires(pkg)) && findBin(fb.bin, pkg.dir, session.workspace.root)) {
    return [basename(fb.bin), ...fb.args].join(' ')
  }
  return null
}

/** Resolve a discovered (non-dev-loop) script in a package. */
export function resolveScript(
  session: Session,
  scriptName: string,
  pkg: PackageInfo,
  extraArgs: string[] = [],
): Resolution {
  if (pkg.scripts[scriptName] == null) {
    return { ok: false, reason: `${pkg.name} has no "${scriptName}" script.` }
  }
  const { file, args } = runScriptCmd(session.workspace.pm, scriptName, extraArgs)
  return {
    ok: true,
    command: { file, args, cwd: pkg.dir, display: withWhere(displayOf(file, args), pkg), source: 'script' },
  }
}
