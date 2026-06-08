import type { PackageInfo } from './types.js'

/**
 * Match a package token against the package list (used by package-scoped verbs
 * and pickers). Exact name match (case-insensitive) wins and short-circuits;
 * otherwise all substring matches are returned for the caller to disambiguate.
 * Pure — unit tested.
 */
export function matchPackages(token: string, packages: PackageInfo[]): PackageInfo[] {
  const lower = token.toLowerCase()
  const exact = packages.filter((p) => p.name.toLowerCase() === lower)
  if (exact.length) return exact

  // Also allow matching the last path segment of a scoped name (@scope/api → api).
  const shortExact = packages.filter((p) => {
    const short = p.name.includes('/') ? p.name.slice(p.name.lastIndexOf('/') + 1) : p.name
    return short.toLowerCase() === lower
  })
  if (shortExact.length) return shortExact

  return packages.filter((p) => p.name.toLowerCase().includes(lower))
}

/**
 * Resolve a verb token to a full verb name via exact match or unambiguous
 * prefix. Returns null when ambiguous or unknown.
 */
export function resolveVerb(token: string, verbNames: string[]): string | null {
  if (verbNames.includes(token)) return token
  const matches = verbNames.filter((n) => n.startsWith(token))
  return matches.length === 1 ? matches[0]! : null
}
