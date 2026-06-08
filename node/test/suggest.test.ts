import { describe, expect, it } from 'vitest'
import { isSuggestDirective, suggestCompletions } from '../src/suggest.js'
import type { PackageInfo, Session } from '../src/types.js'

// A complete-enough session: suggestCompletions reads workspace.packages (for
// the argument position) and runs dev-loop detection (which reads each
// package's scripts/raw + the workspace root package).
function fakeSession(
  packages: { name: string; scripts?: Record<string, string>; raw?: Record<string, unknown> }[],
): Session {
  const pkgs: PackageInfo[] = packages.map((p, i) => ({
    name: p.name,
    dir: `/repo/${p.name}`,
    relDir: i === 0 ? '.' : p.name,
    isRoot: i === 0,
    private: false,
    scripts: p.scripts ?? {},
    raw: p.raw ?? {},
  }))
  return {
    workspace: { root: '/repo', pm: 'pnpm', rootPackage: pkgs[0]!, packages: pkgs, isMonorepo: pkgs.length > 1, orchestrator: null },
    config: {},
    env: undefined,
    flags: { dryRun: false, quiet: false, noEnv: false },
    globalConfigPath: null,
    repoConfigPath: null,
  } as unknown as Session
}

describe('isSuggestDirective', () => {
  it('matches the bare and positioned directive', () => {
    expect(isSuggestDirective('[suggest]')).toBe(true)
    expect(isSuggestDirective('[suggest:12]')).toBe(true)
  })
  it('rejects anything else', () => {
    expect(isSuggestDirective('suggest')).toBe(false)
    expect(isSuggestDirective('[suggest:x]')).toBe(false)
    expect(isSuggestDirective('build')).toBe(false)
    expect(isSuggestDirective(undefined)).toBe(false)
  })
})

describe('suggestCompletions', () => {
  const session = fakeSession([
    { name: 'web', scripts: { dev: 'vite', build: 'vite build', deploy: 'sh deploy' } },
    { name: 'api' },
  ])

  it('offers only applicable verbs + their aliases at the verb position', () => {
    const out = suggestCompletions(session, 'rig ')
    expect(out).toContain('dev') // dev script
    expect(out).toContain('build') // build script
    expect(out).toContain('b') // build's alias (build applies)
    expect(out).toContain('deploy') // discovered script as a verb
    expect(out).toContain('completion') // standalone, always available
    // Deterministic: verbs with no script / dep / config are NOT shown…
    expect(out).not.toContain('lint')
    expect(out).not.toContain('format')
    // …and neither are their aliases.
    expect(out).not.toContain('l')
    expect(out).not.toContain('fmt')
  })

  it('prefix-filters the current verb word', () => {
    const out = suggestCompletions(session, 'rig co')
    expect(out).toContain('coverage')
    expect(out).toContain('completion')
    expect(out).not.toContain('build')
  })

  it('offers workspace package names at the argument position', () => {
    const out = suggestCompletions(session, 'rig build ')
    expect(out).toEqual(['api', 'web'])
  })

  it('filters package names by the partial token', () => {
    expect(suggestCompletions(session, 'rig build a')).toEqual(['api'])
  })

  it('surfaces global flags once the word starts with a dash', () => {
    const out = suggestCompletions(session, 'rig build -')
    expect(out).toContain('--dry-run')
    expect(out).toContain('--quiet')
  })
})
