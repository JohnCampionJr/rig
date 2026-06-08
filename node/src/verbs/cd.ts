import { basename } from 'node:path'
import { isInteractiveErr, selectFrom } from '../prompts.js'
import { ui } from '../ui.js'
import type { PackageInfo, Session } from '../types.js'

/** The segment after the last '/' of a (possibly scoped) package name. */
function shortName(name: string): string {
  const i = name.lastIndexOf('/')
  return i >= 0 ? name.slice(i + 1) : name
}

/** Is `needle` a subsequence of `haystack`? (both already lowercased) */
function isSubsequence(needle: string, haystack: string): boolean {
  if (!needle) return true
  let i = 0
  for (let j = 0; j < haystack.length && i < needle.length; j++) {
    if (haystack[j] === needle[i]) i++
  }
  return i === needle.length
}

/** Score one field against the query: exact > prefix > substring > subsequence. */
function fieldScore(field: string, q: string): number {
  const h = field.toLowerCase()
  if (h === q) return 100
  if (h.startsWith(q)) return 80
  if (h.includes(q)) return 60
  return isSubsequence(q, h) ? 40 : 0
}

/**
 * Score a package as a (best tier, matched-on-name?) pair: the best tier across
 * the name/short-name fields and the path fields (relative path / dir basename),
 * plus whether the name fields were as good as the path fields. A name match
 * outranks a path-only match at the same tier (so `cd web` prefers `web` over
 * `web/__tests__`).
 */
function rank(pkg: PackageInfo, q: string): { best: number; byName: boolean } {
  const nameTier = Math.max(fieldScore(pkg.name, q), fieldScore(shortName(pkg.name), q))
  const pathTier = Math.max(fieldScore(pkg.relDir, q), fieldScore(basename(pkg.dir), q))
  return { best: Math.max(nameTier, pathTier), byName: nameTier > 0 && nameTier >= pathTier }
}

/**
 * Packages matching `query`, best first. Path-aware — matches the name, short
 * name, relative path, or directory basename — and forgiving: exact → prefix →
 * substring → subsequence (`aw` → `apps/web`). Ties break by name-match over
 * path-match, then deepest directory, then shorter name. Pure.
 */
export function rankCdTargets(packages: PackageInfo[], query: string): PackageInfo[] {
  const q = query.trim().toLowerCase()
  if (!q) return [...packages]
  return packages
    .map((p) => ({ p, r: rank(p, q) }))
    .filter((x) => x.r.best > 0)
    .sort(
      (a, b) =>
        b.r.best - a.r.best ||
        Number(b.r.byName) - Number(a.r.byName) ||
        b.p.dir.length - a.p.dir.length ||
        a.p.name.length - b.p.name.length,
    )
    .map((x) => x.p)
}

/** The single best cd target for a query, or null. Pure. */
export function resolveCdTarget(packages: PackageInfo[], query: string): PackageInfo | null {
  return rankCdTargets(packages, query)[0] ?? null
}

/**
 * `rig cd [query]` — print a package directory to stdout; the `rig` shell
 * wrapper (from `rig completion`) does the actual `cd`. With a query it's the
 * best fuzzy match; a query that matches nothing falls back to the picker (so
 * you recover without retyping). Without a query it's the picker straight away.
 * The picker renders to stderr so stdout carries only the path (the wrapper
 * captures it via `$(...)`, where stdout isn't a TTY).
 */
export async function cd(session: Session, query?: string): Promise<number> {
  const packages = session.workspace.packages

  if (query) {
    const target = resolveCdTarget(packages, query)
    if (target) {
      process.stdout.write(`${target.dir}\n`)
      return 0
    }
    if (!isInteractiveErr()) {
      ui.error(`no package matches "${query}".`)
      return 1
    }
    ui.warn(`no package matches "${query}" — pick one:`)
  } else if (!isInteractiveErr()) {
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
