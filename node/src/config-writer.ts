import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { CONFIG_NAME, SCHEMA_URL } from './config.js'
import { isDryRun } from './exec.js'
import { editJsonc } from './jsonc.js'

/**
 * Set a top-level key in the repo's .rig.json, preserving comments and
 * formatting (splice in place). Creates the file with a `$schema` reference if
 * it doesn't exist. This is how `default`/`setup` persist settings.
 */
export function writeRigSetting(root: string, key: string, value: unknown): boolean {
  const path = join(root, CONFIG_NAME)
  const text = existsSync(path) ? safeRead(path) : ''

  const edited = text ? editJsonc(text, key, value) : null
  const next =
    edited ?? JSON.stringify({ $schema: SCHEMA_URL, [key]: value }, null, 2) + '\n'

  if (isDryRun()) return true
  try {
    writeFileSync(path, next, 'utf8')
    return true
  } catch {
    return false
  }
}

function safeRead(path: string): string {
  try {
    return readFileSync(path, 'utf8')
  } catch {
    return ''
  }
}
