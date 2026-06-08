import { pc, ui } from '../ui.js'
import { allScriptNames } from '../discovery.js'
import { DEV_LOOP_VERBS, describeDevLoop } from '../resolve.js'
import type { Session } from '../types.js'

/** `rig info` — show what rig discovered and resolved for this repo. */
export async function info(session: Session): Promise<number> {
  const { workspace, config } = session
  const row = (label: string, value: string) => ui.out(`  ${pc.dim(label.padEnd(14))} ${value}`)

  ui.out(pc.bold(pc.cyan('rig')) + pc.dim(' · discovered'))
  ui.out('')
  row('root', workspace.root)
  row('pm', workspace.pm)
  row('layout', workspace.isMonorepo ? `monorepo (${workspace.packages.length - 1} packages)` : 'single package')
  if (workspace.orchestrator) row('orchestrator', `${workspace.orchestrator} (rig runs its own graph)`)
  row('default', config.defaultProject ?? pc.dim('(none)'))
  row('config', session.repoConfigPath ?? pc.dim('(.rig.json, not yet created)'))
  row('global cfg', session.globalConfigPath ?? pc.dim('(none)'))

  ui.out('')
  ui.out(pc.bold('  packages'))
  for (const p of workspace.packages) {
    const verbs = DEV_LOOP_VERBS.filter((v) => describeDevLoop(session, v, p) != null).map((v) => v.name)
    const tag = p.isRoot ? pc.dim('root') : pc.dim(p.relDir)
    ui.out(`    ${pc.green(p.name)} ${tag}`)
    if (verbs.length) ui.out(`      ${pc.dim('verbs:')} ${verbs.join(', ')}`)
  }

  const scripts = allScriptNames(workspace)
  if (scripts.length) {
    ui.out('')
    ui.out(pc.bold('  scripts') + pc.dim(' (run any as a verb)'))
    ui.out(`    ${scripts.join(', ')}`)
  }
  return 0
}
