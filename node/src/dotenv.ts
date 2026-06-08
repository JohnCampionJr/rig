import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'

/**
 * Parse .env content. Handles comments, `export` prefix, single/double quotes
 * (double quotes honor \n \t \r \" \\), and inline comments on unquoted values.
 * No ${VAR} expansion. Pure — unit tested.
 */
export function parseDotEnv(content: string): Record<string, string> {
  const out: Record<string, string> = {}
  for (const rawLine of content.split(/\r?\n/)) {
    let line = rawLine.trim()
    if (!line || line.startsWith('#')) continue
    if (line.startsWith('export ')) line = line.slice('export '.length).trim()

    const eq = line.indexOf('=')
    if (eq === -1) continue
    const key = line.slice(0, eq).trim()
    if (!key) continue

    let value = line.slice(eq + 1).trim()
    const first = value[0]
    const last = value[value.length - 1]
    if (value.length >= 2 && (first === '"' || first === "'") && last === first) {
      value = value.slice(1, -1)
      if (first === '"') {
        value = value
          .replace(/\\n/g, '\n')
          .replace(/\\t/g, '\t')
          .replace(/\\r/g, '\r')
          .replace(/\\"/g, '"')
          .replace(/\\\\/g, '\\')
      }
    } else {
      const comment = value.indexOf(' #')
      if (comment !== -1) value = value.slice(0, comment).trim()
    }
    out[key] = value
  }
  return out
}

/** Load `.env` then `.env.local` (local wins) from a directory. */
export function loadDotEnvFiles(dir: string): Record<string, string> {
  const merged: Record<string, string> = {}
  for (const name of ['.env', '.env.local']) {
    const path = join(dir, name)
    if (existsSync(path)) {
      try {
        Object.assign(merged, parseDotEnv(readFileSync(path, 'utf8')))
      } catch {
        // ignore unreadable env files
      }
    }
  }
  return merged
}
