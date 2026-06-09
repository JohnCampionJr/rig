import { existsSync, readFileSync } from 'node:fs'
import { basename, dirname, join, relative, sep } from 'node:path'
import { glob } from 'tinyglobby'
import { parse as parseYaml } from 'yaml'
import { detectPm } from './pm.js'
import { matchAnyGlob } from './glob.js'
import type { PackageInfo, Workspace } from './types.js'

function readJson(path: string): Record<string, unknown> | null {
  try {
    return JSON.parse(readFileSync(path, 'utf8')) as Record<string, unknown>
  } catch {
    return null
  }
}

function toPackageInfo(dir: string, root: string, raw: Record<string, unknown>): PackageInfo {
  const rel = relative(root, dir)
  return {
    name: typeof raw.name === 'string' && raw.name ? raw.name : basename(dir),
    dir,
    relDir: rel === '' ? '.' : rel,
    isRoot: dir === root,
    private: raw.private === true,
    scripts:
      raw.scripts && typeof raw.scripts === 'object'
        ? (raw.scripts as Record<string, string>)
        : {},
    raw,
  }
}

/** Read the workspace package globs from pnpm-workspace.yaml or package.json. */
export function workspaceGlobs(root: string, rootPkg: Record<string, unknown>): string[] {
  const pnpmFile = join(root, 'pnpm-workspace.yaml')
  if (existsSync(pnpmFile)) {
    try {
      const doc = parseYaml(readFileSync(pnpmFile, 'utf8')) as { packages?: unknown }
      if (Array.isArray(doc?.packages)) return doc.packages.filter((p): p is string => typeof p === 'string')
    } catch {
      // fall through
    }
  }
  const ws = rootPkg.workspaces
  if (Array.isArray(ws)) return ws.filter((p): p is string => typeof p === 'string')
  if (ws && typeof ws === 'object' && Array.isArray((ws as { packages?: unknown }).packages)) {
    return (ws as { packages: unknown[] }).packages.filter((p): p is string => typeof p === 'string')
  }
  return []
}

function detectOrchestrator(root: string): 'turbo' | 'nx' | null {
  if (existsSync(join(root, 'turbo.json'))) return 'turbo'
  if (existsSync(join(root, 'nx.json'))) return 'nx'
  return null
}

/**
 * Discover the workspace: package manager, root package, and all member
 * packages (root first), honoring `exclude` globs against name and relDir.
 */
export async function discoverWorkspace(root: string, exclude: string[] = []): Promise<Workspace> {
  const rootPkgRaw = readJson(join(root, 'package.json')) ?? {}
  const { pm, agent } = await detectPm(root)
  const rootPackage = toPackageInfo(root, root, rootPkgRaw)

  const globs = workspaceGlobs(root, rootPkgRaw)
  const members: PackageInfo[] = []
  if (globs.length) {
    // Negation globs (leading '!') are exclusions in pnpm/yarn workspace specs.
    const positive = globs.filter((g) => !g.startsWith('!'))
    const negated = globs.filter((g) => g.startsWith('!')).map((g) => g.slice(1))
    const patterns = positive.map((g) => `${g.replace(/\/$/, '')}/package.json`)
    const ignore = negated.map((g) => `${g.replace(/\/$/, '')}/package.json`)
    const matches = await glob(patterns, {
      cwd: root,
      ignore: ['**/node_modules/**', ...ignore],
      absolute: true,
      dot: false,
    })
    const seen = new Set<string>([root])
    for (const pkgJsonPath of matches.sort()) {
      const dir = dirname(pkgJsonPath)
      if (seen.has(dir)) continue
      seen.add(dir)
      const raw = readJson(pkgJsonPath)
      if (raw) members.push(toPackageInfo(dir, root, raw))
    }
  }

  const filtered = exclude.length
    ? members.filter((p) => !matchAnyGlob(exclude, p.name) && !matchAnyGlob(exclude, p.relDir))
    : members

  return {
    root,
    pm,
    agent,
    rootPackage,
    packages: [rootPackage, ...filtered],
    isMonorepo: filtered.length > 0,
    orchestrator: detectOrchestrator(root),
  }
}

/**
 * The member package you're "in": the deepest non-root package whose directory
 * contains `cwd`. Null at the workspace root (or in a non-package subdir), so
 * the caller falls back to the configured default / picker. Drives monorepo
 * cwd-awareness — `rig test` inside `packages/web` targets web.
 */
export function currentPackage(packages: PackageInfo[], cwd: string): PackageInfo | null {
  let best: PackageInfo | null = null
  for (const p of packages) {
    if (p.isRoot) continue
    if (cwd === p.dir || cwd.startsWith(p.dir + sep)) {
      if (!best || p.dir.length > best.dir.length) best = p
    }
  }
  return best
}

/**
 * The union of script names across all packages (for menu/Scripts group and
 * dynamic verb resolution).
 */
export function allScriptNames(ws: Workspace): string[] {
  const names = new Set<string>()
  for (const pkg of ws.packages) for (const name of Object.keys(pkg.scripts)) names.add(name)
  return [...names].sort()
}
