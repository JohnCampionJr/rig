import { readFileSync } from 'node:fs'
import { globalAddCmd } from '../pm.js'
import { run } from '../exec.js'
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

/** `rig update [--check]` — update rig to the latest published version. */
export async function update(session: Session, opts: { check?: boolean } = {}): Promise<number> {
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
  if (opts.check) return 0

  const { file, args } = globalAddCmd(session.workspace.agent, `${PACKAGE}@latest`)
  return run(file, args, { env: session.env })
}
