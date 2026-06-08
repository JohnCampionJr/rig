import { describe, expect, it } from 'vitest'
import { buildOptions } from '../src/menu.js'
import type { PackageInfo, Session } from '../src/types.js'

function mkPkg(over: Partial<PackageInfo>): PackageInfo {
  return { name: 'p', dir: '/repo/p', relDir: 'p', isRoot: false, private: false, scripts: {}, raw: {}, ...over }
}

const root = mkPkg({ name: 'root', dir: '/repo', relDir: '.', isRoot: true })
const web = mkPkg({ name: 'web', dir: '/repo/packages/web', relDir: 'packages/web', scripts: { dev: 'vite', build: 'vite build', deploy: 'sh deploy' } })
const api = mkPkg({ name: 'api', dir: '/repo/packages/api', relDir: 'packages/api', scripts: { test: 'vitest run' } })

function session(currentPackage: PackageInfo | null): Session {
  return {
    workspace: { root: '/repo', pm: 'pnpm', rootPackage: root, packages: [root, web, api], isMonorepo: true, orchestrator: null },
    currentPackage,
    config: {},
    env: undefined,
    flags: { dryRun: false, quiet: false, noEnv: false },
    globalConfigPath: null,
    repoConfigPath: null,
  } as unknown as Session
}

const verbNames = (opts: ReturnType<typeof buildOptions>) =>
  opts.filter((o) => o.value.kind === 'verb').map((o) => (o.value as { name: string }).name)
const focusOpt = (opts: ReturnType<typeof buildOptions>) =>
  opts.find((o) => o.value.kind === 'focus') as { value: { kind: 'focus'; focus: PackageInfo | null }; label: string } | undefined

describe('menu buildOptions — focus', () => {
  it('focused on a package shows only its verbs + a "whole repo" switch', () => {
    const opts = buildOptions(session(web), web)
    expect(verbNames(opts).sort()).toEqual(['build', 'dev']) // web's scripts only — no test (that's api)
    const f = focusOpt(opts)
    expect(f?.value.focus).toBeNull()
    expect(f?.label).toContain('whole repo')
    // its own scripts are surfaced
    expect(opts.some((o) => o.value.kind === 'scripts')).toBe(true)
  })

  it('whole-repo (focus null) shows the union and a switch back to the current package', () => {
    const opts = buildOptions(session(web), null)
    // union across web + api
    expect(verbNames(opts)).toContain('dev')
    expect(verbNames(opts)).toContain('build')
    expect(verbNames(opts)).toContain('test')
    const f = focusOpt(opts)
    expect(f?.value.focus).toBe(web)
    expect(f?.label).toContain('web')
  })

  it('at the root (no current package) there is no focus switch', () => {
    const opts = buildOptions(session(null), null)
    expect(focusOpt(opts)).toBeUndefined()
  })
})
