import { describe, expect, it } from 'vitest'
import { matchPackages, resolveVerb } from '../src/fuzzy.js'
import { currentPackage } from '../src/discovery.js'
import { resolveTarget } from '../src/target.js'
import { buildKillPatterns, parsePids } from '../src/verbs/kill.js'
import { addCmd, detectPackageManager, dlxCmd, runScriptCmd } from '../src/pm.js'
import { CLEAN_DIRS, cleanCandidates } from '../src/verbs/maintenance.js'
import { parseLinePct } from '../src/verbs/coverage.js'
import { compareVersions, isNewer } from '../src/verbs/update.js'
import { filterPackages, topoSort } from '../src/graph.js'
import type { PackageInfo, Session } from '../src/types.js'

function pkg(name: string, dir: string, isRoot = false): PackageInfo {
  return { name, dir, relDir: isRoot ? '.' : dir, isRoot, private: false, scripts: {}, raw: {} }
}

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
    workspace: { root: '/repo', pm: 'pnpm', rootPackage: PKGS[0]!, packages: PKGS, isMonorepo: true, orchestrator: null },
    currentPackage: null,
    config: {},
    env: undefined,
    flags: { dryRun: false, quiet: false, noEnv: false },
    globalConfigPath: null,
    repoConfigPath: null,
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
  it('current package is ignored when it cannot run the verb (not a candidate)', () => {
    // in web, but only api is a candidate → fall through to the sole candidate
    const r = resolveTarget(session({ currentPackage: PKGS[1]! }), [PKGS[2]!])
    expect(r.kind === 'pkg' && r.pkg.name).toBe('@app/api')
  })
})

describe('currentPackage', () => {
  it('finds the deepest member containing cwd', () => {
    expect(currentPackage(PKGS, '/repo/apps/web/src/x')?.name).toBe('@app/web')
    expect(currentPackage(PKGS, '/repo/apps/api')?.name).toBe('@app/api')
  })
  it('is null at the root or in a non-package subdir', () => {
    expect(currentPackage(PKGS, '/repo')).toBeNull()
    expect(currentPackage(PKGS, '/repo/apps')).toBeNull()
  })
  it('does not match a sibling by name prefix', () => {
    const pkgs = [pkg('root', '/repo', true), pkg('@x/web', '/repo/web'), pkg('@x/web2', '/repo/web2')]
    expect(currentPackage(pkgs, '/repo/web2/src')?.name).toBe('@x/web2')
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
  it('detects packageManager field', () => {
    expect(detectPackageManager('/x', { packageManager: 'pnpm@9.0.0' })).toBe('pnpm')
    expect(detectPackageManager('/x', {})).toBe('npm')
  })
  it('runScriptCmd inserts -- for npm only', () => {
    expect(runScriptCmd('npm', 'dev', ['--host'])).toEqual({ file: 'npm', args: ['run', 'dev', '--', '--host'] })
    expect(runScriptCmd('pnpm', 'dev', ['--host'])).toEqual({ file: 'pnpm', args: ['run', 'dev', '--host'] })
  })
  it('addCmd dev flag per pm', () => {
    expect(addCmd('pnpm', 'vitest', true)).toEqual({ file: 'pnpm', args: ['add', '-D', 'vitest'] })
    expect(addCmd('npm', 'vitest', false)).toEqual({ file: 'npm', args: ['install', '--save', 'vitest'] })
  })
  it('dlxCmd per pm', () => {
    expect(dlxCmd('bun', 'cowsay', ['hi'])).toEqual({ file: 'bunx', args: ['cowsay', 'hi'] })
    expect(dlxCmd('pnpm', 'cowsay')).toEqual({ file: 'pnpm', args: ['dlx', 'cowsay'] })
  })
})

describe('cleanCandidates', () => {
  it('lists every CLEAN_DIR under every package', () => {
    const out = cleanCandidates([PKGS[0]!, PKGS[1]!])
    expect(out).toContain('/repo/dist')
    expect(out).toContain('/repo/apps/web/.turbo')
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
})
