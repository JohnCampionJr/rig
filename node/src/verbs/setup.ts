import { writeRigSetting } from '../config-writer.js'
import { isInteractive, pickPackage, prompt, BACK } from '../prompts.js'
import { ui } from '../ui.js'
import type { Session } from '../types.js'

/** `rig setup` — interactive walkthrough that writes prefs to .rig.json. */
export async function setup(session: Session): Promise<number> {
  if (!isInteractive()) {
    ui.error('setup is interactive; run it in a terminal.')
    return 1
  }

  const { workspace, config } = session
  const root = workspace.root
  prompt.intro('rig setup')

  // Default package (monorepos only).
  if (workspace.packages.length > 1) {
    const picked = await pickPackage(
      workspace.packages,
      config.defaultProject,
      'Default package? (Esc to skip)',
    )
    if (picked && picked !== BACK) {
      writeRigSetting(root, 'defaultProject', picked.name)
      ui.success(`default package → ${picked.name}`)
    }
  }

  // Quiet.
  const quiet = await prompt.confirm({
    message: 'Suppress the “→ command” echo by default?',
    initialValue: config.quiet ?? false,
  })
  if (!prompt.isCancel(quiet)) {
    writeRigSetting(root, 'quiet', quiet)
    ui.success(`quiet → ${quiet}`)
  }

  prompt.outro('saved to .rig.json')
  return 0
}
