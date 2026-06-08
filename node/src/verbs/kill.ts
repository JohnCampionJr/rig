import { matchPackages } from '../fuzzy.js'
import { capture, isDryRun, run } from '../exec.js'
import { ui } from '../ui.js'
import type { Session } from '../types.js'

/**
 * Build the command-line patterns to match for `kill`. With a token, target
 * the matching packages' directories; otherwise use configured patterns, then
 * fall back to the candidate package directories. Pure — unit tested.
 */
export function buildKillPatterns(session: Session, token?: string): string[] {
  const { workspace, config } = session
  if (token) {
    const matches = matchPackages(token, workspace.packages)
    return matches.map((p) => p.dir)
  }
  if (config.kill?.match?.length) return config.kill.match
  // Default: every non-root package dir, or the root in a single-package repo.
  const members = workspace.packages.filter((p) => !p.isRoot)
  return (members.length ? members : workspace.packages).map((p) => p.dir)
}

/** Parse `lsof -ti` (or similar) output into a unique, sorted PID list. Pure. */
export function parsePids(output: string): number[] {
  const pids = output
    .split(/\s+/)
    .map((s) => Number.parseInt(s, 10))
    .filter((n) => Number.isInteger(n) && n > 0 && n !== process.pid)
  return [...new Set(pids)].sort((a, b) => a - b)
}

function listeningPids(port: number): number[] {
  if (process.platform === 'win32') {
    const out = capture('cmd', ['/c', `netstat -ano -p tcp | findstr :${port}`])
    if (!out) return []
    const pids = out
      .split(/\r?\n/)
      .filter((l) => /LISTENING/i.test(l))
      .map((l) => Number.parseInt(l.trim().split(/\s+/).pop() ?? '', 10))
      .filter((n) => Number.isInteger(n) && n > 0)
    return [...new Set(pids)]
  }
  const out = capture('lsof', ['-ti', `tcp:${port}`, '-sTCP:LISTEN'])
  return out ? parsePids(out) : []
}

async function killPids(pids: number[]): Promise<number> {
  if (pids.length === 0) return 0
  if (process.platform === 'win32') {
    let code = 0
    for (const pid of pids) {
      code = (await run('taskkill', ['/PID', String(pid), '/F'])) || code
    }
    return code
  }
  return run('kill', pids.map(String))
}

export interface KillOptions {
  token?: string
  ports?: number[]
}

/** `rig kill` — terminate dev-server processes by port or command-line pattern. */
export async function kill(session: Session, opts: KillOptions = {}): Promise<number> {
  // Numeric token is treated as a port.
  const ports = [...(opts.ports ?? [])]
  let token = opts.token
  if (token && /^\d+$/.test(token)) {
    ports.push(Number.parseInt(token, 10))
    token = undefined
  }

  if (ports.length) {
    const pids = [...new Set(ports.flatMap(listeningPids))]
    if (pids.length === 0) {
      ui.info(`nothing listening on ${ports.join(', ')}`)
      return 0
    }
    if (isDryRun()) {
      ui.info(`would kill pids on ${ports.join(', ')}: ${pids.join(', ')}`)
      return 0
    }
    ui.command(`kill ${pids.join(' ')}`)
    const code = await killPids(pids)
    if (code === 0) ui.success(`killed ${pids.length} process(es)`)
    return code
  }

  // Pattern-based kill (Unix: pkill/pgrep; matches full command line).
  const patterns = buildKillPatterns(session, token)
  if (patterns.length === 0) {
    ui.error('nothing to kill (no matching packages or patterns)')
    return 1
  }

  if (process.platform === 'win32') {
    ui.warn('pattern-based kill is Unix-only; use `rig kill --port <n>` on Windows.')
    return 1
  }

  let killed = 0
  for (const pattern of patterns) {
    if (isDryRun()) {
      const out = capture('pgrep', ['-fl', pattern])
      if (out?.trim()) {
        ui.info(`would kill (matching ${pattern}):`)
        ui.out(out.trimEnd())
      }
      continue
    }
    ui.command(`pkill -f ${pattern}`)
    const code = await run('pkill', ['-f', pattern])
    // pkill exit 0 = killed something, 1 = no match (fine).
    if (code === 0) killed++
  }
  if (!isDryRun()) {
    if (killed) ui.success('done')
    else ui.info('no matching processes')
  }
  return 0
}
