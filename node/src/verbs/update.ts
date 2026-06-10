import { readFileSync } from 'node:fs'
import { globalAddCmd } from '../pm.js'
import { run, displayOf } from '../exec.js'
import { findDotnetTool } from '../delegate.js'
import { ui } from '../ui.js'
import type { Session } from '../types.js'

const PACKAGE = '@jcamp/rig'

/** Compare two `major.minor.patch` versions (prerelease/build ignored). Pure. */
export function compareVersions(a: string, b: string): number {
  const parse = (v: string) =>
    v
      .replace(/^v/, '')
      .split(/[-+]/)[0]!
      .split('.')
      .map((n) => Number.parseInt(n, 10) || 0)
  const pa = parse(a)
  const pb = parse(b)
  for (let i = 0; i < 3; i++) {
    const diff = (pa[i] ?? 0) - (pb[i] ?? 0)
    if (diff !== 0) return diff < 0 ? -1 : 1
  }
  return 0
}

/** True when `remote` is strictly newer than `current`. Pure. */
export function isNewer(current: string, remote: string): boolean {
  return compareVersions(current, remote) < 0
}

function currentVersion(): string {
  try {
    const pkg = JSON.parse(readFileSync(new URL('../../package.json', import.meta.url), 'utf8')) as {
      version?: string
    }
    return pkg.version ?? '0.0.0'
  } catch {
    return '0.0.0'
  }
}

async function latestVersion(): Promise<string | null> {
  try {
    const res = await fetch(`https://registry.npmjs.org/${PACKAGE}/latest`)
    if (!res.ok) return null
    const data = (await res.json()) as { version?: string }
    return data.version ?? null
  } catch {
    return null
  }
}

/**
 * Args for handing off to the sibling tool's self-update. Always carries
 * `--self-only` so the sibling never cross-updates back to us. Pure.
 */
export function siblingArgs(check?: boolean): string[] {
  return check ? ['self-update', '--check', '--self-only'] : ['self-update', '--self-only']
}

async function updateSelf(session: Session, check?: boolean): Promise<number> {
  const current = currentVersion()
  const latest = await latestVersion()
  if (!latest) {
    ui.warn(`could not reach the npm registry (or ${PACKAGE} is not published yet).`)
    return 1
  }
  if (!isNewer(current, latest)) {
    ui.success(`rig is up to date (${current})`)
    return 0
  }

  ui.info(`update available: ${current} → ${latest}`)
  if (check) return 0

  const { file, args } = globalAddCmd(session.workspace.agent, `${PACKAGE}@latest`)
  return run(file, args, { env: session.env })
}

async function updateSibling(session: Session, check?: boolean): Promise<number> {
  const tool = findDotnetTool()
  if (!tool) {
    ui.info("the .NET rig isn't installed — nothing else to update.")
    return 0
  }
  const args = siblingArgs(check)
  ui.info(check ? 'checking the .NET rig…' : 'updating the .NET rig…')
  // RIG_NO_DELEGATE so the .NET tool runs natively instead of handing back to us.
  return run(tool, args, {
    env: { ...(session.env ?? {}), RIG_NO_DELEGATE: '1' },
    display: displayOf('rig', args),
  })
}

/**
 * `rig self-update [--check] [--self-only]` — update rig to the latest published
 * version. The two tools ship in lockstep, so by default this also updates the
 * sibling .NET rig when it's installed (handed off with `--self-only`, so it
 * can't bounce back). `--self-only` updates just this ecosystem.
 */
export async function update(
  session: Session,
  opts: { check?: boolean; selfOnly?: boolean } = {},
): Promise<number> {
  const selfCode = await updateSelf(session, opts.check)
  if (opts.selfOnly) return selfCode
  const siblingCode = await updateSibling(session, opts.check)
  return selfCode !== 0 ? selfCode : siblingCode
}
