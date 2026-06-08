import { existsSync, readFileSync } from 'node:fs'
import { homedir } from 'node:os'
import { join } from 'node:path'
import { parseJsonc } from './jsonc.js'
import { ui } from './ui.js'
import type { RigConfig } from './types.js'

/** The single rig config file (JSONC: comments + trailing commas allowed). */
export const CONFIG_NAME = 'rig.config.json'

/** Public URL of the JSON Schema, for the `$schema` key (editor autocomplete). */
export const SCHEMA_URL =
  'https://raw.githubusercontent.com/JohnCampionJr/rig/main/node/rig.schema.json'

function isUnset(value: unknown): boolean {
  return value == null || (typeof value === 'string' && value.trim() === '')
}

function unionDict<T>(
  base: Record<string, T> | undefined,
  overlay: Record<string, T> | undefined,
): Record<string, T> | undefined {
  if (!base && !overlay) return undefined
  return { ...(base ?? {}), ...(overlay ?? {}) }
}

/**
 * Merge two configs: overlay wins per scalar key (empty strings count as
 * unset), dictionaries union, arrays concat-dedupe. Pure — unit tested.
 */
export function mergeConfig(base: RigConfig, overlay: RigConfig): RigConfig {
  const result: RigConfig = { ...base }

  if (!isUnset(overlay.defaultProject)) result.defaultProject = overlay.defaultProject
  if (overlay.quiet != null) result.quiet = overlay.quiet

  result.env = unionDict(base.env, overlay.env)
  result.commands = unionDict(base.commands, overlay.commands)
  result.aliases = unionDict(base.aliases, overlay.aliases)
  result.envPresets = unionDict(base.envPresets, overlay.envPresets)

  if (base.exclude || overlay.exclude) {
    result.exclude = [...new Set([...(base.exclude ?? []), ...(overlay.exclude ?? [])])]
  }
  if (base.coverage || overlay.coverage) {
    result.coverage = { ...(base.coverage ?? {}), ...(overlay.coverage ?? {}) }
  }
  if (base.kill || overlay.kill) {
    result.kill = {
      match: [...new Set([...(base.kill?.match ?? []), ...(overlay.kill?.match ?? [])])],
    }
  }
  return result
}

/** Read and parse a rig.config.json at `path` (tolerant of comments). */
function readConfigFile(path: string): RigConfig {
  try {
    const value = parseJsonc<unknown>(readFileSync(path, 'utf8'))
    if (value && typeof value === 'object') {
      const { $schema, ...rest } = value as RigConfig & { $schema?: string }
      void $schema
      return rest
    }
  } catch {
    ui.warn(`could not parse ${path}; ignoring it.`)
  }
  return {}
}

/** The global config path: $RIG_GLOBAL_CONFIG, else ~/.rig.json. */
export function globalConfigPath(): string {
  return process.env.RIG_GLOBAL_CONFIG || join(homedir(), '.rig.json')
}

export interface LoadedConfig {
  config: RigConfig
  /** Loaded global config path, or null if absent. */
  globalConfigPath: string | null
  /** Repo rig.config.json path (the writable file); null if it doesn't exist yet. */
  repoConfigPath: string | null
}

/**
 * Load and merge config: global (~/.rig.json or $RIG_GLOBAL_CONFIG) → repo
 * rig.config.json (repo wins per key; dictionaries union).
 */
export function loadConfig(root: string): LoadedConfig {
  const globalPath = globalConfigPath()
  const globalExists = existsSync(globalPath)
  const repoPath = join(root, CONFIG_NAME)
  const repoExists = existsSync(repoPath)

  let config: RigConfig = {}
  if (globalExists) config = mergeConfig(config, readConfigFile(globalPath))
  if (repoExists) config = mergeConfig(config, readConfigFile(repoPath))

  return {
    config,
    globalConfigPath: globalExists ? globalPath : null,
    repoConfigPath: repoExists ? repoPath : null,
  }
}
