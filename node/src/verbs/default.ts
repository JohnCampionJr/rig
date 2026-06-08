import { writeRigSetting } from '../config-writer.js'
import { matchPackages } from '../fuzzy.js'
import { BACK, isInteractive, pickPackage } from '../prompts.js'
import { ui } from '../ui.js'
import type { Session } from '../types.js'

/**
 * `rig default [project]` — show or set the default package for package-scoped
 * verbs. Writes to the `rig` block of package.json.
 */
export async function setDefault(session: Session, token?: string): Promise<number> {
  const packages = session.workspace.packages
  const current = session.config.defaultProject

  // No argument: show (or prompt to set) the default.
  if (!token) {
    if (!isInteractive()) {
      ui.out(current ?? '(none)')
      return 0
    }
    ui.info(current ? `current default: ${current}` : 'no default set')
    const picked = await pickPackage(packages, current, 'Set default package to?')
    if (!picked || picked === BACK) return 1
    return persist(session, picked.name)
  }

  const matches = matchPackages(token, packages)
  if (matches.length === 0) {
    ui.error(`no package matches "${token}"`)
    return 1
  }
  if (matches.length > 1) {
    if (!isInteractive()) {
      ui.error(`"${token}" matches several packages; be more specific.`)
      return 1
    }
    const picked = await pickPackage(matches, current, 'Which package?')
    if (!picked || picked === BACK) return 1
    return persist(session, picked.name)
  }
  return persist(session, matches[0]!.name)
}

function persist(session: Session, name: string): number {
  const ok = writeRigSetting(session.workspace.root, 'defaultProject', name)
  if (!ok) {
    ui.error('could not write to package.json')
    return 1
  }
  ui.success(`default package set to ${name}`)
  return 0
}
