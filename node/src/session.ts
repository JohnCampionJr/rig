import { loadConfig } from './config.js'
import { discoverWorkspace } from './discovery.js'
import { loadDotEnvFiles } from './dotenv.js'
import { resolveRoot } from './root.js'
import type { Flags, RigConfig, Session } from './types.js'

/**
 * Build the env overlay: .env/.env.local values apply only where the ambient
 * env doesn't already define them (ambient wins over file), then config.env
 * applies on top (config wins over everything). Returns undefined when empty.
 */
export function buildEnv(
  root: string,
  config: RigConfig,
  useDotEnv: boolean,
): Record<string, string> | undefined {
  const overlay: Record<string, string> = {}
  if (useDotEnv) {
    const fileEnv = loadDotEnvFiles(root)
    for (const [key, value] of Object.entries(fileEnv)) {
      if (!(key in process.env)) overlay[key] = value
    }
  }
  for (const [key, value] of Object.entries(config.env ?? {})) {
    overlay[key] = value
  }
  return Object.keys(overlay).length ? overlay : undefined
}

/** Resolve root, load+merge config, discover the workspace, and build env. */
export async function loadSession(flags: Flags, cwd: string = process.cwd()): Promise<Session> {
  const root = resolveRoot(cwd)
  const { config, globalConfigPath, repoConfigPath } = loadConfig(root)
  const workspace = await discoverWorkspace(root, config.exclude)
  const env = buildEnv(root, config, !flags.noEnv)

  return {
    workspace,
    config,
    env,
    flags,
    globalConfigPath,
    repoConfigPath,
  }
}
