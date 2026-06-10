import { describe, expect, it } from 'vitest'
import { matchPackages, resolveVerb } from '../src/fuzzy.js'
import { currentPackage } from '../src/discovery.js'
import { resolveCdTarget } from '../src/verbs/cd.js'
import { resolveTarget } from '../src/target.js'
import { buildKillPatterns, parsePids } from '../src/verbs/kill.js'
import { addCmd, executeCmd, runScriptCmd, toPackageManager } from '../src/pm.js'
import { CLEAN_DIRS, cleanCandidates } from '../src/verbs/maintenance.js'
import { parseLinePct } from '../src/verbs/coverage.js'
import { compareVersions, isNewer, siblingArgs } from '../src/verbs/update.js'
import { filterPackages, topoSort } from '../src/graph.js'
import type { PackageInfo, Session } from '../src/types.js'
import { join, sep } from 'node:path'

function pkg(name: string, dir: string, isRoot = false): PackageInfo {
  return { name, dir, relDir: isRoot ? '.' : dir, isRoot, private: false, scripts: {}, raw: {} }
}

// Build an OS-native path *under* a package dir, keeping the (POSIX-literal) dir
// itself intact, so currentPackage's `dir + sep` prefix check matches on Windows
// too — production paths use the OS separator, but these test dirs use `/`.
const under = (dir: string, ...parts: string[]) => [dir, ...parts].join(sep)

const PKGS = [
  pkg('root', '/repo', true),
  pkg('@app/web', '/repo/apps/web'),
  pkg('@app/api', '/repo/apps/api'),
]

describe('matchPackages', () => {
  it('matches exact name', () => {
    expect(matchPackages('@app/web', PKGS).map((p) => p.name)).toEqual(['@app/web'])
  })
  it('matches short (last path segment) of scoped name', () => {
    expect(matchPackages('api', PKGS).map((p) => p.name)).toEqual(['@app/api'])
  })
  it('falls back to substring', () => {
    expect(matchPackages('app', PKGS).map((p) => p.name).sort()).toEqual(['@app/api', '@app/web'])
  })
})

describe('resolveVerb', () => {
  it('exact and unambiguous prefix', () => {
    expect(resolveVerb('build', ['build', 'test'])).toBe('build')
    expect(resolveVerb('bu', ['build', 'test'])).toBe('build')
    expect(resolveVerb('t', ['test', 'typecheck'])).toBeNull()
  })
})

function session(over: Partial<Session> = {}): Session {
  return {
    workspace: { root: '/repo', pm: 'pnpm', agent: 'pnpm', rootPackage: PKGS[0]!, packages: PKGS, isMonorepo: true, orchestrator: null },
    currentPackage: null,
    config: {},
    env: undefined,
    flags: { dryRun: false, quiet: false, noEnv: false, root: false },
    globalConfigPath: null,
    repoConfigPath: null,
    viteplusTool: null,
    ...over,
  }
}

describe('resolveTarget', () => {
  it('token → single match', () => {
    const r = resolveTarget(session(), PKGS, 'web')
    expect(r.kind === 'pkg' && r.pkg.name).toBe('@app/web')
  })
  it('token → multiple → pick', () => {
    const r = resolveTarget(session(), PKGS, 'app')
    expect(r.kind).toBe('pick')
  })
  it('no token + default → that package', () => {
    const r = resolveTarget(session({ config: { defaultProject: 'api' } }), PKGS)
    expect(r.kind === 'pkg' && r.pkg.name).toBe('@app/api')
  })
  it('no token + single candidate → that package', () => {
    const one = [PKGS[1]!]
    const r = resolveTarget(session(), one)
    expect(r.kind === 'pkg' && r.pkg.name).toBe('@app/web')
  })
  it('no token + many + no default → pick', () => {
    expect(resolveTarget(session(), PKGS).kind).toBe('pick')
  })

  it('no token + current package (cwd) wins over the picker', () => {
    const r = resolveTarget(session({ currentPackage: PKGS[1]! }), PKGS)
    expect(r.kind === 'pkg' && r.pkg.name).toBe('@app/web')
  })
  it('current package beats the configured default', () => {
    const r = resolveTarget(session({ currentPackage: PKGS[1]!, config: { defaultProject: 'api' } }), PKGS)
    expect(r.kind === 'pkg' && r.pkg.name).toBe('@app/web')
  })
  it('inside a package that cannot run the verb → pick, never auto-pick another', () => {
    // in web, but only api is a candidate → do NOT silently run api; pick instead
    const r = resolveTarget(session({ currentPackage: PKGS[1]! }), [PKGS[2]!])
    expect(r.kind).toBe('pick')
  })
  it('at the root, a sole candidate still auto-resolves', () => {
    // no currentPackage (root / --root) → the convenience stays
    const r = resolveTarget(session(), [PKGS[2]!])
    expect(r.kind === 'pkg' && r.pkg.name).toBe('@app/api')
  })
})

describe('resolveCdTarget', () => {
  it('matches a package and returns its dir', () => {
    expect(resolveCdTarget(PKGS, 'web')?.dir).toBe('/repo/apps/web')
    expect(resolveCdTarget(PKGS, 'api')?.dir).toBe('/repo/apps/api')
  })
  it('returns null when nothing matches', () => {
    expect(resolveCdTarget(PKGS, 'zzz')).toBeNull()
  })
  it('prefers the deepest dir when several match equally', () => {
    const nested = [pkg('@x/foo', '/repo/foo'), pkg('@x/foobar', '/repo/foo/bar')]
    // "oo" is a substring of both names → the deeper directory wins
    expect(resolveCdTarget(nested, 'oo')?.dir).toBe('/repo/foo/bar')
  })
  it('is path-aware: matches the directory even when the name differs', () => {
    const p = pkg('@acme/core', '/repo/libs/dashboard') // name has no "dashboard"
    expect(resolveCdTarget([p], 'dashboard')?.dir).toBe('/repo/libs/dashboard')
  })
  it('matches a subsequence (aw → apps/web)', () => {
    expect(resolveCdTarget([pkg('@x/web', '/repo/apps/web')], 'aw')?.dir).toBe('/repo/apps/web')
  })
  it('an exact short-name match outranks a substring match', () => {
    const pkgs = [pkg('@x/api', '/repo/api'), pkg('@x/api-client', '/repo/api-client')]
    expect(resolveCdTarget(pkgs, 'api')?.name).toBe('@x/api')
  })
  it('a name match outranks a path-only match (web vs tests/web)', () => {
    const pkgs = [pkg('@x/web', '/repo/apps/web'), pkg('@x/web-tests', '/repo/tests/web')]
    // both have a "web" directory basename; @x/web also matches by name → it wins
    expect(resolveCdTarget(pkgs, 'web')?.name).toBe('@x/web')
  })
})

describe('currentPackage', () => {
  it('finds the deepest member containing cwd', () => {
    expect(currentPackage(PKGS, under('/repo/apps/web', 'src', 'x'))?.name).toBe('@app/web')
    expect(currentPackage(PKGS, '/repo/apps/api')?.name).toBe('@app/api')
  })
  it('is null at the root or in a non-package subdir', () => {
    expect(currentPackage(PKGS, '/repo')).toBeNull()
    expect(currentPackage(PKGS, '/repo/apps')).toBeNull()
  })
  it('does not match a sibling by name prefix', () => {
    const pkgs = [pkg('root', '/repo', true), pkg('@x/web', '/repo/web'), pkg('@x/web2', '/repo/web2')]
    expect(currentPackage(pkgs, under('/repo/web2', 'src'))?.name).toBe('@x/web2')
  })
})

describe('kill helpers', () => {
  it('parsePids dedupes, filters, sorts, drops self', () => {
    expect(parsePids(`123\n45\n123\n${process.pid}\nx`)).toEqual([45, 123])
  })
  it('buildKillPatterns: token → matching package dirs', () => {
    expect(buildKillPatterns(session(), 'web')).toEqual(['/repo/apps/web'])
  })
  it('buildKillPatterns: bare → non-root package dirs', () => {
    expect(buildKillPatterns(session())).toEqual(['/repo/apps/web', '/repo/apps/api'])
  })
  it('buildKillPatterns: configured patterns win', () => {
    expect(buildKillPatterns(session({ config: { kill: { match: ['node dev'] } } }))).toEqual(['node dev'])
  })
})

describe('pm command builders', () => {
  it('maps detector agents to the supported set (deno/unknown → npm)', () => {
    expect(toPackageManager('pnpm')).toBe('pnpm')
    expect(toPackageManager('yarn')).toBe('yarn')
    expect(toPackageManager('bun')).toBe('bun')
    expect(toPackageManager('deno')).toBe('npm')
    expect(toPackageManager(undefined)).toBe('npm')
  })
  it('runScriptCmd inserts -- for npm only', () => {
    expect(runScriptCmd('npm', 'dev', ['--host'])).toEqual({ file: 'npm', args: ['run', 'dev', '--', '--host'] })
    expect(runScriptCmd('pnpm', 'dev', ['--host'])).toEqual({ file: 'pnpm', args: ['run', 'dev', '--host'] })
  })
  it('addCmd dev flag per pm', () => {
    expect(addCmd('pnpm', 'vitest', true)).toEqual({ file: 'pnpm', args: ['add', '-D', 'vitest'] })
    expect(addCmd('npm', 'vitest', false)).toEqual({ file: 'npm', args: ['install', '--save', 'vitest'] })
  })
  it('executeCmd per agent (ni-parity table; full coverage in ni-parity.test.ts)', () => {
    expect(executeCmd('bun', 'cowsay', ['hi'])).toEqual({ file: 'bun', args: ['x', 'cowsay', 'hi'] })
    expect(executeCmd('pnpm', 'cowsay')).toEqual({ file: 'pnpm', args: ['dlx', 'cowsay'] })
  })
})

describe('cleanCandidates', () => {
  it('lists every CLEAN_DIR under every package', () => {
    const out = cleanCandidates([PKGS[0]!, PKGS[1]!])
    expect(out).toContain(join('/repo', 'dist'))
    expect(out).toContain(join('/repo/apps/web', '.turbo'))
    expect(out.length).toBe(2 * CLEAN_DIRS.length)
  })
})

describe('parseLinePct', () => {
  it('reads total.lines.pct', () => {
    expect(parseLinePct('{"total":{"lines":{"pct":87.5}}}')).toBe(87.5)
  })
  it('returns null when missing or invalid', () => {
    expect(parseLinePct('{"total":{}}')).toBeNull()
    expect(parseLinePct('not json')).toBeNull()
  })
})

describe('topoSort', () => {
  const mk = (name: string, deps: Record<string, string> = {}): PackageInfo => ({
    name,
    dir: `/repo/${name}`,
    relDir: name,
    isRoot: false,
    private: false,
    scripts: {},
    raw: { dependencies: deps },
  })

  it('orders dependencies before dependents', () => {
    // app depends on ui, ui depends on core
    const pkgs = [mk('app', { ui: '1' }), mk('ui', { core: '1' }), mk('core')]
    const order = topoSort(pkgs).map((p) => p.name)
    expect(order.indexOf('core')).toBeLessThan(order.indexOf('ui'))
    expect(order.indexOf('ui')).toBeLessThan(order.indexOf('app'))
  })

  it('ignores deps outside the set and tolerates cycles', () => {
    const pkgs = [mk('a', { b: '1', react: '1' }), mk('b', { a: '1' })]
    expect(topoSort(pkgs).map((p) => p.name).sort()).toEqual(['a', 'b'])
  })

  it('filterPackages matches name or relDir', () => {
    const pkgs = [mk('@x/web'), mk('@x/api')]
    expect(filterPackages(pkgs, 'web').map((p) => p.name)).toEqual(['@x/web'])
  })
})

describe('version compare', () => {
  it('orders by major.minor.patch', () => {
    expect(compareVersions('1.2.3', '1.2.3')).toBe(0)
    expect(compareVersions('1.2.3', '1.2.4')).toBe(-1)
    expect(compareVersions('2.0.0', '1.9.9')).toBe(1)
  })
  it('ignores v prefix and prerelease/build', () => {
    expect(compareVersions('v1.0.0', '1.0.0-beta.1')).toBe(0)
  })
  it('isNewer is strict', () => {
    expect(isNewer('1.0.0', '1.0.1')).toBe(true)
    expect(isNewer('1.0.1', '1.0.1')).toBe(false)
    expect(isNewer('1.0.2', '1.0.1')).toBe(false)
  })
  it('siblingArgs always carries --self-only so the cross-update never bounces back', () => {
    expect(siblingArgs(false)).toEqual(['self-update', '--self-only'])
    expect(siblingArgs(true)).toEqual(['self-update', '--check', '--self-only'])
  })
})
