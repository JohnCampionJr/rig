import { matchPackages } from './fuzzy.js'
import type { PackageInfo, Session } from './types.js'

export type TargetResult =
  | { kind: 'pkg'; pkg: PackageInfo }
  | { kind: 'pick'; packages: PackageInfo[] }
  | { kind: 'none'; reason: string }

/**
 * Resolve which package a package-scoped verb targets, given the candidate set
 * and an optional token. Token → fuzzy match (one hit runs, many → pick). Pure.
 *
 * No token, and you're *inside* a package (monorepo cwd-awareness): run that
 * package if it can do the verb; otherwise never silently act on another — pick
 * (or error). No token at the root / `--root` / single-package repo: the
 * configured default, else the sole candidate, else pick.
 */
export function resolveTarget(
  session: Session,
  candidates: PackageInfo[],
  token?: string,
): TargetResult {
  if (token) {
    const matches = matchPackages(token, candidates)
    if (matches.length === 1) return { kind: 'pkg', pkg: matches[0]! }
    if (matches.length === 0) return { kind: 'none', reason: `no runnable package matches "${token}"` }
    return { kind: 'pick', packages: matches }
  }

  if (candidates.length === 0) return { kind: 'none', reason: 'no runnable package found' }

  // Inside a package: act on it if it can run the verb; otherwise require an
  // explicit choice rather than auto-picking a different package.
  const current = session.currentPackage
  if (current) {
    if (candidates.some((c) => c.dir === current.dir)) return { kind: 'pkg', pkg: current }
    return { kind: 'pick', packages: candidates }
  }

  // At the root / --root / single-package: default → sole → pick.
  const def = session.config.defaultProject
  if (def) {
    const matches = matchPackages(def, candidates)
    if (matches.length === 1) return { kind: 'pkg', pkg: matches[0]! }
  }
  if (candidates.length === 1) return { kind: 'pkg', pkg: candidates[0]! }
  return { kind: 'pick', packages: candidates }
}
