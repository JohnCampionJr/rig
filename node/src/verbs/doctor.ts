import { existsSync } from 'node:fs'
import { join } from 'node:path'
import { capture } from '../exec.js'
import { pc, ui } from '../ui.js'
import type { Session } from '../types.js'

type Level = 'ok' | 'warn' | 'error'

function line(level: Level, label: string, detail: string) {
  const mark = level === 'ok' ? pc.green('✓') : level === 'warn' ? pc.yellow('!') : pc.red('✗')
  ui.out(`  ${mark} ${label.padEnd(12)} ${pc.dim(detail)}`)
}

/** Parse the minimum major version out of an engines range like ">=18.17". */
function minMajor(range: string): number | null {
  const match = range.match(/(\d+)/)
  return match ? Number.parseInt(match[1]!, 10) : null
}

/** `rig doctor` — flag environment problems (node version, pm, install state). */
export async function doctor(session: Session): Promise<number> {
  const { workspace } = session
  let severity = 0 // 0 ok · 1 warn · 2 error
  const bump = (l: Level) => {
    severity = Math.max(severity, l === 'error' ? 2 : l === 'warn' ? 1 : 0)
  }

  ui.out(pc.bold(pc.cyan('rig')) + pc.dim(' · doctor'))
  ui.out('')

  // Node version vs engines.
  const current = process.versions.node
  const engines = (workspace.rootPackage.raw.engines as { node?: string } | undefined)?.node
  if (engines) {
    const need = minMajor(engines)
    const have = Number.parseInt(current.split('.')[0]!, 10)
    if (need != null && have < need) {
      line('error', 'node', `${current} — package requires node ${engines}`)
      bump('error')
    } else {
      line('ok', 'node', `${current} (requires ${engines})`)
    }
  } else {
    line('ok', 'node', current)
  }

  // Package manager available.
  const pmVersion = capture(workspace.pm, ['--version'])
  if (pmVersion) {
    line('ok', 'pm', `${workspace.pm} ${pmVersion.trim()}`)
  } else {
    line('error', 'pm', `${workspace.pm} not found on PATH`)
    bump('error')
  }

  // Dependencies installed.
  if (existsSync(join(workspace.root, 'node_modules'))) {
    line('ok', 'install', 'node_modules present')
  } else {
    line('warn', 'install', `not installed — run \`rig install\``)
    bump('warn')
  }

  // Workspace / orchestrator info.
  line('ok', 'layout', workspace.isMonorepo ? `monorepo, ${workspace.packages.length - 1} packages` : 'single package')
  if (workspace.orchestrator) {
    line('warn', 'orchestrator', `${workspace.orchestrator} present — rig runs its own graph`)
    bump('warn')
  }

  ui.out('')
  if (severity === 0) ui.success('all good')
  else if (severity === 1) ui.warn('some warnings')
  else ui.error('problems found')
  return severity === 2 ? 1 : 0
}
