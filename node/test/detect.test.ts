import { describe, expect, it } from 'vitest'
import { applicableDevLoopVerbs, canRunVerb, getDevLoopVerb, resolveScriptName } from '../src/resolve.js'
import type { PackageInfo, Session } from '../src/types.js'

// Detection is driven by package.json (scripts + declared deps) plus config
// files. These fixtures use scripts and declared deps so they're filesystem-
// independent; the config-gated paths (lint/format) rely on the fake dirs not
// existing, so the eslint/prettier-config gate fails as intended.
function mkPkg(over: Partial<PackageInfo> = {}): PackageInfo {
  return { name: 'pkg', dir: '/repo', relDir: '.', isRoot: true, private: false, scripts: {}, raw: {}, ...over }
}
function mkSession(pkgs: PackageInfo[]): Session {
  return {
    workspace: { root: '/repo', pm: 'pnpm', rootPackage: pkgs[0]!, packages: pkgs, isMonorepo: pkgs.length > 1, orchestrator: null },
    config: {},
    env: undefined,
    flags: { dryRun: false, quiet: false, noEnv: false },
    globalConfigPath: null,
    repoConfigPath: null,
  } as unknown as Session
}

describe('resolveScriptName — placeholder test', () => {
  it('returns a real script', () => {
    expect(resolveScriptName(mkPkg({ scripts: { test: 'vitest run' } }), ['test'])).toBe('test')
  })
  it('skips the npm `npm init` placeholder test script', () => {
    const pkg = mkPkg({ scripts: { test: 'echo "Error: no test specified" && exit 1' } })
    expect(resolveScriptName(pkg, ['test'])).toBeNull()
  })
})

describe('canRunVerb — declared-or-installed detection', () => {
  const dev = getDevLoopVerb('dev')!
  const test = getDevLoopVerb('test')!
  const lint = getDevLoopVerb('lint')!

  it('a matching script makes a verb apply', () => {
    const pkg = mkPkg({ scripts: { dev: 'vite' } })
    expect(canRunVerb(mkSession([pkg]), dev, pkg)).toBe(true)
  })

  it('dev does not apply without a dev/start script (no tool fallback)', () => {
    const pkg = mkPkg({ raw: { devDependencies: { vite: '^5' } } })
    expect(canRunVerb(mkSession([pkg]), dev, pkg)).toBe(false)
  })

  it('a declared test runner makes `test` apply with no script', () => {
    const pkg = mkPkg({ raw: { devDependencies: { vitest: '^1' } } })
    expect(canRunVerb(mkSession([pkg]), test, pkg)).toBe(true)
  })

  it('a tool declared on the workspace root counts for a member package', () => {
    const root = mkPkg({ name: 'root', raw: { devDependencies: { jest: '^29' } } })
    const member = mkPkg({ name: 'web', dir: '/repo/web', relDir: 'web', isRoot: false })
    expect(canRunVerb(mkSession([root, member]), test, member)).toBe(true)
  })

  it('the placeholder test + no runner → `test` does not apply', () => {
    const pkg = mkPkg({ scripts: { test: 'echo "Error: no test specified" && exit 1' } })
    expect(canRunVerb(mkSession([pkg]), test, pkg)).toBe(false)
  })

  it('lint needs a config file even when eslint is a declared dep', () => {
    // No eslint config on the (non-existent) fake dir → the fallback is gated out.
    const pkg = mkPkg({ raw: { devDependencies: { eslint: '^9' } } })
    expect(canRunVerb(mkSession([pkg]), lint, pkg)).toBe(false)
  })

  it('a lint script applies regardless of config (script wins)', () => {
    const pkg = mkPkg({ scripts: { lint: 'eslint .' } })
    expect(canRunVerb(mkSession([pkg]), lint, pkg)).toBe(true)
  })
})

describe('applicableDevLoopVerbs', () => {
  it('lists only the verbs that apply somewhere in the workspace', () => {
    const pkg = mkPkg({ scripts: { dev: 'vite' }, raw: { devDependencies: { vitest: '^1' } } })
    const names = applicableDevLoopVerbs(mkSession([pkg])).map((v) => v.name)
    expect(names).toContain('dev')
    expect(names).toContain('test')
    expect(names).not.toContain('lint')
    expect(names).not.toContain('format')
    expect(names).not.toContain('typecheck')
  })
})
