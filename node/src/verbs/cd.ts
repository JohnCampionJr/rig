import { matchPackages } from '../fuzzy.js'
import { isInteractiveErr, selectFrom } from '../prompts.js'
import { ui } from '../ui.js'
import type { PackageInfo, Session } from '../types.js'

/**
 * The package directory to cd into for a query: the best fuzzy match, and the
 * deepest one when several match equally ("the deepest point that works"). Pure.
 */
export function resolveCdTarget(packages: PackageInfo[], query: string): PackageInfo | null {
  const matches = matchPackages(query, packages)
  if (matches.length === 0) return null
  return [...matches].sort((a, b) => b.dir.length - a.dir.length)[0]!
}

/**
 * `rig cd [query]` — print a package directory to stdout; the `rig` shell
 * wrapper (from `rig completion`) does the actual `cd`. With a query it's the
 * best fuzzy match; without one, an interactive picker. The picker renders to
 * stderr so stdout carries only the path (the wrapper captures it via `$(...)`,
 * where stdout isn't a TTY — hence the stderr-based interactivity check).
 */
export async function cd(session: Session, query?: string): Promise<number> {
  const packages = session.workspace.packages

  if (query) {
    const target = resolveCdTarget(packages, query)
    if (!target) {
      ui.error(`no package matches "${query}".`)
      return 1
    }
    process.stdout.write(`${target.dir}\n`)
    return 0
  }

  if (!isInteractiveErr()) {
    ui.error('rig cd: name a package, or run in a terminal to pick one.')
    return 1
  }
  const picked = await selectFrom(
    'cd to which package?',
    packages.map((p) => ({ value: p, label: p.isRoot ? `${p.name} (root)` : p.name, hint: p.relDir })),
    session.currentPackage ?? undefined,
    { output: process.stderr },
  )
  if (!picked) return 1
  process.stdout.write(`${picked.dir}\n`)
  return 0
}
