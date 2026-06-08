/** Shared domain types for rig. */

export type PackageManager = 'npm' | 'pnpm' | 'yarn' | 'bun'

/** A single package.json in the repo (root package or a workspace member). */
export interface PackageInfo {
  /** `name` field, or the directory name when unnamed. */
  name: string
  /** Absolute directory containing the package.json. */
  dir: string
  /** Path relative to the workspace root (''/'.' for the root package). */
  relDir: string
  /** Whether this is the workspace root package. */
  isRoot: boolean
  /** `private: true` in package.json. */
  private: boolean
  /** The `scripts` block, verbatim. */
  scripts: Record<string, string>
  /** Raw parsed package.json (for dependency graph, etc.). */
  raw: Record<string, unknown>
}

/** The discovered shape of the repo. */
export interface Workspace {
  /** Absolute repo root (where discovery anchored). */
  root: string
  /** Detected package manager. */
  pm: PackageManager
  /** Root package.json. */
  rootPackage: PackageInfo
  /** All packages (root + workspace members), root first. */
  packages: PackageInfo[]
  /** True when workspace globs resolved to >0 member packages. */
  isMonorepo: boolean
  /** Whether a turbo.json / nx.json orchestrator is present (informational). */
  orchestrator: 'turbo' | 'nx' | null
}

/** Coverage configuration.
 * No `full`: that's a .NET-only knob (ReportGenerator's single-file vs multi-file HTML).
 * Node delegates coverage to vitest/jest, whose report shape lives in their own config. */
export interface CoverageConfig {
  open?: boolean
  min?: number
}

/** A custom command definition (string, argv array, or per-OS variants). */
export type CommandDef =
  | string
  | string[]
  | {
      command?: string | string[]
      os?: { macos?: string | string[]; linux?: string | string[]; windows?: string | string[] }
      env?: Record<string, string>
      cwd?: string
      description?: string
    }

/** .rig.json shape — every field optional. */
export interface RigConfig {
  /** Default package for package-scoped verbs when several are runnable. */
  defaultProject?: string
  /** Glob patterns hiding packages from pickers. */
  exclude?: string[]
  /** Suppress the `→ command` echo. */
  quiet?: boolean
  /** Env applied to every spawned command. */
  env?: Record<string, string>
  /** Named env presets, e.g. test.envPresets.log → `rig test --log`. */
  envPresets?: Record<string, Record<string, string>>
  coverage?: CoverageConfig
  /** Extra command-line patterns for `kill` to match. */
  kill?: { match?: string[] }
  /** Custom verbs (generalizes package.json scripts). */
  commands?: Record<string, CommandDef>
  /** Override a verb's short alias. */
  aliases?: Record<string, string>
}

/** Global flags threaded through every verb. */
export interface Flags {
  dryRun: boolean
  quiet: boolean
  noEnv: boolean
}

/** The resolved context handed to every verb. */
export interface Session {
  workspace: Workspace
  config: RigConfig
  /** Merged env overlay (or undefined when nothing to override). */
  env: Record<string, string> | undefined
  flags: Flags
  /** Path of the loaded global config (~/.rig.json), if any (for `info`). */
  globalConfigPath: string | null
  /** Path of the repo .rig.json, if it exists (for `info`). */
  repoConfigPath: string | null
}
