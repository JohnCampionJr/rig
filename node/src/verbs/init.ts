import { existsSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { CONFIG_NAME, SCHEMA_URL } from '../config.js'
import { isDryRun } from '../exec.js'
import { ui } from '../ui.js'
import type { Session } from '../types.js'

const template = () => `{
  "$schema": "${SCHEMA_URL}",

  // Default package for package-scoped verbs when several are runnable.
  // "defaultProject": "web",

  // Hide packages from pickers (glob against name or relative path).
  // "exclude": ["*-bench", "examples/*"],

  // Env applied to every spawned command (.env / .env.local load automatically).
  // "env": { "FORCE_COLOR": "1" },

  // Named env presets, e.g. \`rig test --log\`.
  // "envPresets": { "log": { "DEBUG": "app:*" } },

  // Suppress the \`→ command\` echo.
  // "quiet": false
}
`

/** `rig init` — scaffold a commented rig.config.json at the repo root. */
export async function init(session: Session): Promise<number> {
  const target = join(session.workspace.root, CONFIG_NAME)
  if (existsSync(target)) {
    ui.warn(`${CONFIG_NAME} already exists at ${session.workspace.root}`)
    return 1
  }
  ui.command(`write ${target}`)
  if (!isDryRun()) writeFileSync(target, template(), 'utf8')
  ui.success(`created ${CONFIG_NAME}`)
  return 0
}
