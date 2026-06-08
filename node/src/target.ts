import { matchPackages } from './fuzzy.js'
import type { PackageInfo, Session } from './types.js'

export type TargetResult =
  | { kind: 'pkg'; pkg: PackageInfo }
  | { kind: 'pick'; packages: PackageInfo[] }
  | { kind: 'none'; reason: string }

/**
 * Resolve which package a package-scoped verb targets, given the candidate set
 * and an optional token. Token → fuzzy match (one hit runs, many → pick).
 * No token → defaultProject, else the sole candidate, else pick. Pure.
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

  const def = session.config.defaultProject
  if (def) {
    const matches = matchPackages(def, candidates)
    if (matches.length === 1) return { kind: 'pkg', pkg: matches[0]! }
  }

  if (candidates.length === 1) return { kind: 'pkg', pkg: candidates[0]! }
  if (candidates.length === 0) return { kind: 'none', reason: 'no runnable package found' }
  return { kind: 'pick', packages: candidates }
}
