import { existsSync } from 'node:fs'
import { basename, join } from 'node:path'
import { findBin, runScriptCmd } from './pm.js'
import { displayOf } from './exec.js'
import type { PackageInfo, Session } from './types.js'

/**
 * A tool fallback: the bin to invoke when no script matches. A verb "makes
 * sense" for a package when the tool is *declared* (a dependency) or *installed*
 * (in node_modules/.bin) — read from package.json, not guessed — and any config
 * gate (a tsconfig / eslint / prettier config must exist) is satisfied.
 */
interface ToolFallback {
  /** The bin to run + the name probed in node_modules/.bin. */
  bin: string
  /** The npm dependency that signals intent; defaults to `bin` (tsc → typescript). */
  pkg?: string
  args: string[]
  /** Config-file gate beyond the tool being present. */
  requires?: (pkg: PackageInfo) => boolean
}

/** Definition of a known dev-loop verb. */
export interface DevLoopVerb {
  name: string
  /** Candidate script names, in priority order. */
  scripts: string[]
  /** Tools to detect & invoke (in priority order) when no script matches. */
  fallbacks: ToolFallback[]
}

// --- package.json / config-file signals ------------------------------------

const DEP_FIELDS = ['dependencies', 'devDependencies', 'optionalDependencies', 'peerDependencies']

/** Is `name` listed in any of `raw`'s dependency maps? */
function declaresDep(raw: Record<string, unknown>, name: string): boolean {
  return DEP_FIELDS.some((field) => {
    const deps = raw[field]
    return deps != null && typeof deps === 'object' && name in (deps as Record<string, unknown>)
  })
}

/** Tool declared as a dependency of the package or the workspace root. */
function toolDeclared(session: Session, pkg: PackageInfo, depName: string): boolean {
  return declaresDep(pkg.raw, depName) || declaresDep(session.workspace.rootPackage.raw, depName)
}

/** True if any of the named files exists in the package dir. */
function hasFile(pkg: PackageInfo, ...names: string[]): boolean {
  return names.some((n) => existsSync(join(pkg.dir, n)))
}

function hasTsconfig(pkg: PackageInfo): boolean {
  return hasFile(pkg, 'tsconfig.json')
}

function hasEslintConfig(pkg: PackageInfo): boolean {
  return (
    pkg.raw.eslintConfig != null ||
    hasFile(
      pkg,
      'eslint.config.js', 'eslint.config.mjs', 'eslint.config.cjs',
      'eslint.config.ts', 'eslint.config.mts', 'eslint.config.cts',
      '.eslintrc', '.eslintrc.js', '.eslintrc.cjs', '.eslintrc.json',
      '.eslintrc.yml', '.eslintrc.yaml',
    )
  )
}

function hasPrettierConfig(pkg: PackageInfo): boolean {
  return (
    pkg.raw.prettier != null ||
    hasFile(
      pkg,
      '.prettierrc', '.prettierrc.json', '.prettierrc.json5', '.prettierrc.yml',
      '.prettierrc.yaml', '.prettierrc.toml', '.prettierrc.js', '.prettierrc.cjs',
      '.prettierrc.mjs', 'prettier.config.js', 'prettier.config.cjs', 'prettier.config.mjs',
    )
  )
}

/**
 * The known dev-loop verbs (scripts-first, detect-as-fallback). Order is the
 * user's working frequency — it drives the menu and `info` listing order.
 */
export const DEV_LOOP_VERBS: DevLoopVerb[] = [
  { name: 'dev', scripts: ['dev', 'start'], fallbacks: [] },
  {
    name: 'typecheck',
    scripts: ['typecheck', 'type-check'],
    fallbacks: [{ bin: 'tsc', pkg: 'typescript', args: ['--noEmit'], requires: hasTsconfig }],
  },
  {
    name: 'lint',
    scripts: ['lint'],
    fallbacks: [{ bin: 'eslint', args: ['.'], requires: hasEslintConfig }],
  },
  {
    name: 'build',
    scripts: ['build'],
    fallbacks: [{ bin: 'tsc', pkg: 'typescript', args: [], requires: hasTsconfig }],
  },
  {
    name: 'test',
    scripts: ['test'],
    fallbacks: [{ bin: 'vitest', args: ['run'] }, { bin: 'jest', args: [] }],
  },
  {
    name: 'format',
    scripts: ['format', 'fmt'],
    fallbacks: [{ bin: 'prettier', args: ['--write', '.'], requires: hasPrettierConfig }],
  },
]

export function getDevLoopVerb(name: string): DevLoopVerb | undefined {
  return DEV_LOOP_VERBS.find((v) => v.name === name)
}

/** npm's `npm init` placeholder — a `test` script that just errors. Not a real verb. */
const PLACEHOLDER_TEST = /no test specified/i

/**
 * The first candidate script the package actually defines, skipping npm's
 * default `test` placeholder (so `test` only shows with a real test setup).
 */
export function resolveScriptName(pkg: PackageInfo, candidates: string[]): string | null {
  for (const name of candidates) {
    const body = pkg.scripts[name]
    if (body != null && !PLACEHOLDER_TEST.test(body)) return name
  }
  return null
}

/** Does a fallback tool apply to the package? (config gate + declared-or-installed.) */
function fallbackApplies(session: Session, fb: ToolFallback, pkg: PackageInfo): boolean {
  if (fb.requires && !fb.requires(pkg)) return false
  return (
    toolDeclared(session, pkg, fb.pkg ?? fb.bin) ||
    findBin(fb.bin, pkg.dir, session.workspace.root) != null
  )
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

  // Detect-as-fallback: among the fallbacks that apply (config gate + declared
  // or installed), run the first that's actually installed.
  const applicable = verb.fallbacks.filter((fb) => fallbackApplies(session, fb, pkg))
  for (const fb of applicable) {
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

  // Applies (declared) but nothing is installed → tell the user to install,
  // rather than blindly invoking a missing or parent-tree tool.
  if (applicable.length) {
    const dep = applicable[0]!.pkg ?? applicable[0]!.bin
    return {
      ok: false,
      reason: `${pkg.name}: ${dep} is a dependency but isn't installed — run \`${session.workspace.pm} install\`.`,
    }
  }

  return { ok: false, reason: explainUnavailable(verb, pkg.name) }
}

/** True when the verb can run for this package (script present or a tool applies). */
export function canRunVerb(session: Session, verb: DevLoopVerb, pkg: PackageInfo): boolean {
  if (resolveScriptName(pkg, verb.scripts)) return true
  return verb.fallbacks.some((fb) => fallbackApplies(session, fb, pkg))
}

/** Packages a verb can run against (for pickers and capability gating). */
export function candidatePackages(session: Session, verb: DevLoopVerb): PackageInfo[] {
  return session.workspace.packages.filter((p) => canRunVerb(session, verb, p))
}

/** Dev-loop verbs that apply to at least one package — what the surface should show. */
export function applicableDevLoopVerbs(session: Session): DevLoopVerb[] {
  return DEV_LOOP_VERBS.filter((v) => candidatePackages(session, v).length > 0)
}

/**
 * Why a dev-loop verb isn't available — for the menu/`info` (per package) and
 * the smart "unknown verb" hint (workspace-wide, `subject = "here"`).
 */
export function explainUnavailable(verb: DevLoopVerb, subject: string): string {
  const scriptList = verb.scripts.map((s) => `"${s}"`).join(' or ')
  const tools = [...new Set(verb.fallbacks.map((f) => f.pkg ?? f.bin))]
  const toolNote = tools.length ? `, and no ${tools.join('/')} dependency` : ''
  return `${verb.name} isn't available (${subject}): no ${scriptList} script${toolNote}.`
}

/**
 * A short, human-facing description of what a dev-loop verb will do for a
 * package — the *underlying* command, so menus teach the real tool.
 * e.g. "vite" (script body) or "tsc --noEmit" (tool fallback).
 */
export function describeDevLoop(session: Session, verb: DevLoopVerb, pkg: PackageInfo): string | null {
  const scriptName = resolveScriptName(pkg, verb.scripts)
  if (scriptName) return pkg.scripts[scriptName] ?? scriptName
  const fb = verb.fallbacks.find((f) => fallbackApplies(session, f, pkg))
  return fb ? [basename(fb.bin), ...fb.args].join(' ') : null
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
