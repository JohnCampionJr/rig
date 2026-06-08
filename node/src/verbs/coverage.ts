import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { displayOf, openPath } from '../exec.js'
import { findBin, runScriptCmd } from '../pm.js'
import { pickTargetPackage, runCommand } from '../dispatch.js'
import { resolveScriptName, type ResolvedCommand } from '../resolve.js'
import { pc, ui } from '../ui.js'
import type { PackageInfo, Session } from '../types.js'

const COVERAGE_SCRIPTS = ['coverage', 'test:coverage', 'cov']

/** Resolve the coverage command for a package: script first, else vitest/jest. */
function resolveCoverage(session: Session, pkg: PackageInfo, extraArgs: string[]): ResolvedCommand | null {
  const script = resolveScriptName(pkg, COVERAGE_SCRIPTS)
  if (script) {
    const { file, args } = runScriptCmd(session.workspace.pm, script, extraArgs)
    return { file, args, cwd: pkg.dir, display: displayOf(file, args), source: 'script' }
  }
  const vitest = findBin('vitest', pkg.dir, session.workspace.root)
  if (vitest) {
    const args = ['run', '--coverage', ...extraArgs]
    return { file: vitest, args, cwd: pkg.dir, display: displayOf('vitest', args), source: 'tool' }
  }
  const jest = findBin('jest', pkg.dir, session.workspace.root)
  if (jest) {
    const args = ['--coverage', ...extraArgs]
    return { file: jest, args, cwd: pkg.dir, display: displayOf('jest', args), source: 'tool' }
  }
  return null
}

/** Whether a package can produce coverage (script or vitest/jest installed). */
export function canCover(session: Session, pkg: PackageInfo): boolean {
  if (resolveScriptName(pkg, COVERAGE_SCRIPTS)) return true
  return (
    findBin('vitest', pkg.dir, session.workspace.root) != null ||
    findBin('jest', pkg.dir, session.workspace.root) != null
  )
}

/** Total line-coverage percent from a coverage-summary.json string, or null. Pure. */
export function parseLinePct(summaryJson: string): number | null {
  try {
    const data = JSON.parse(summaryJson) as { total?: { lines?: { pct?: number } } }
    const pct = data.total?.lines?.pct
    return typeof pct === 'number' ? pct : null
  } catch {
    return null
  }
}

/** Locate a coverage HTML report under a package dir. */
function findReport(pkgDir: string): string | null {
  for (const rel of ['coverage/index.html', 'coverage/lcov-report/index.html']) {
    const path = join(pkgDir, rel)
    if (existsSync(path)) return path
  }
  return null
}

export interface CoverageOptions {
  token?: string
  open?: boolean
  min?: number
  extraArgs?: string[]
  env?: Record<string, string>
}

/** `rig coverage [project]` — run coverage, optionally open the report and gate on --min. */
export async function coverage(session: Session, opts: CoverageOptions = {}): Promise<number> {
  const candidates = session.workspace.packages.filter((p) => canCover(session, p))
  if (candidates.length === 0) {
    ui.error('no package has a coverage script or vitest/jest installed.')
    return 1
  }
  const pkg = await pickTargetPackage(session, candidates, 'coverage', opts.token)
  if (!pkg) return 1

  const command = resolveCoverage(session, pkg, opts.extraArgs ?? [])
  if (!command) {
    ui.error(`${pkg.name} has no coverage script and no vitest/jest installed.`)
    return 1
  }

  const code = await runCommand(session, command, opts.env)
  if (code !== 0) return code

  const min = opts.min
  if (min != null) {
    const summaryPath = join(pkg.dir, 'coverage', 'coverage-summary.json')
    if (existsSync(summaryPath)) {
      const pct = parseLinePct(readFileSync(summaryPath, 'utf8'))
      if (pct != null) {
        ui.info(`line coverage: ${pct.toFixed(2)}%`)
        if (pct < min) {
          ui.error(`coverage ${pct.toFixed(2)}% is below the --min ${min}% threshold`)
          return 1
        }
      }
    } else {
      ui.warn('--min needs a json-summary reporter (coverage/coverage-summary.json not found).')
    }
  }

  if (opts.open) {
    const report = findReport(pkg.dir)
    if (report) await openPath(report)
    else ui.dim(pc.dim('no HTML report found to open'))
  }
  return 0
}
