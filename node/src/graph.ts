import { matchGlob } from './glob.js'
import type { PackageInfo } from './types.js'

function depNames(pkg: PackageInfo): string[] {
  const raw = pkg.raw
  const merged = {
    ...((raw.dependencies as Record<string, string>) ?? {}),
    ...((raw.devDependencies as Record<string, string>) ?? {}),
    ...((raw.peerDependencies as Record<string, string>) ?? {}),
  }
  return Object.keys(merged)
}

/**
 * Topologically sort packages so that intra-workspace dependencies come before
 * the packages that depend on them. Only edges *within the given set* count.
 * Cycles are tolerated (a back-edge is skipped); order is otherwise stable.
 * Pure — unit tested.
 */
export function topoSort(packages: PackageInfo[]): PackageInfo[] {
  const byName = new Map(packages.map((p) => [p.name, p]))
  const deps = new Map<string, string[]>()
  for (const p of packages) {
    deps.set(
      p.name,
      depNames(p).filter((d) => d !== p.name && byName.has(d)),
    )
  }

  const result: PackageInfo[] = []
  const done = new Set<string>()
  const onStack = new Set<string>()

  const visit = (name: string) => {
    if (done.has(name) || onStack.has(name)) return
    onStack.add(name)
    for (const dep of deps.get(name) ?? []) visit(dep)
    onStack.delete(name)
    done.add(name)
    const pkg = byName.get(name)
    if (pkg) result.push(pkg)
  }

  for (const p of packages) visit(p.name)
  return result
}

/**
 * Filter packages by a glob (matched against name or relative path) or, for
 * convenience, a case-insensitive substring of the name.
 */
export function filterPackages(packages: PackageInfo[], filter: string): PackageInfo[] {
  const lower = filter.toLowerCase()
  return packages.filter(
    (p) => matchGlob(filter, p.name) || matchGlob(filter, p.relDir) || p.name.toLowerCase().includes(lower),
  )
}
