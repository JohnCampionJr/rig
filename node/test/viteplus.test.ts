import { afterEach, describe, expect, it } from 'vitest'
import {
  hasViteplusDep,
  mapVerb,
  resolveViteplusTool,
  viteplusCommandWith,
  VP_VERBS,
} from '../src/viteplus.js'
import type { PackageInfo, Workspace } from '../src/types.js'

function mkPkg(raw: Record<string, unknown> = {}): PackageInfo {
  return { name: 'root', dir: '/repo', relDir: '.', isRoot: true, private: false, scripts: {}, raw }
}
function mkWorkspace(raw: Record<string, unknown> = {}): Workspace {
  const root = mkPkg(raw)
  return { root: '/repo', pm: 'pnpm', agent: 'pnpm', rootPackage: root, packages: [root], isMonorepo: false, orchestrator: null }
}

afterEach(() => {
  delete process.env.RIG_VP_TOOL
})

describe('mapVerb — rig verb → vp subcommand', () => {
  it('renames the verbs that differ', () => {
    expect(mapVerb('format')).toBe('fmt')
    expect(mapVerb('uninstall')).toBe('remove')
    expect(mapVerb('upgrade')).toBe('update')
  })
  it('passes through the verbs that match', () => {
    for (const v of ['dev', 'build', 'test', 'lint', 'install', 'add', 'outdated', 'dlx']) {
      expect(mapVerb(v)).toBe(VP_VERBS[v])
    }
  })
  it('does NOT map typecheck — `vp check` also lints+formats, so it is not a pure typecheck', () => {
    expect(mapVerb('typecheck')).toBeNull()
  })
  it('returns null for verbs with no vp analog (incl. global/ci, kept native)', () => {
    for (const v of ['kill', 'cd', 'doctor', 'coverage', 'clean', 'global', 'ci']) {
      expect(mapVerb(v)).toBeNull()
    }
  })
})

describe('hasViteplusDep — the opt-in signal', () => {
  it('true when vite-plus is a devDependency', () => {
    expect(hasViteplusDep(mkPkg({ devDependencies: { 'vite-plus': '^1' } }))).toBe(true)
  })
  it('true when vite-plus is a (prod) dependency', () => {
    expect(hasViteplusDep(mkPkg({ dependencies: { 'vite-plus': '^1' } }))).toBe(true)
  })
  it('false for a plain Vite project (vite but not vite-plus)', () => {
    expect(hasViteplusDep(mkPkg({ devDependencies: { vite: '^7' } }))).toBe(false)
  })
})

describe('viteplusCommandWith — the pure builder', () => {
  it('builds `vp <sub> <args>` from a resolved tool', () => {
    expect(viteplusCommandWith('/bin/vp', 'test', [])).toEqual({ file: '/bin/vp', args: ['test'] })
    expect(viteplusCommandWith('/bin/vp', 'uninstall', ['lodash', '--filter', 'web'])).toEqual({
      file: '/bin/vp',
      args: ['remove', 'lodash', '--filter', 'web'],
    })
  })
  it('null when there is no tool (not a Vite+ repo / vp missing)', () => {
    expect(viteplusCommandWith(null, 'test')).toBeNull()
  })
  it('null when the verb has no vp analog, even with a tool', () => {
    expect(viteplusCommandWith('/bin/vp', 'typecheck')).toBeNull()
    expect(viteplusCommandWith('/bin/vp', 'kill')).toBeNull()
  })
})

describe('resolveViteplusTool — detection (dep + binary)', () => {
  it('returns the tool when vite-plus is declared and vp resolves', () => {
    process.env.RIG_VP_TOOL = process.execPath // any existing file proves "vp is installed"
    expect(resolveViteplusTool(mkWorkspace({ devDependencies: { 'vite-plus': '^1' } }))).toBe(process.execPath)
  })
  it('null when there is no vite-plus dep (even if vp is on the machine)', () => {
    process.env.RIG_VP_TOOL = process.execPath
    expect(resolveViteplusTool(mkWorkspace({ devDependencies: { vite: '^7' } }))).toBeNull()
  })
  it('null when vite-plus is declared but the vp binary is missing', () => {
    process.env.RIG_VP_TOOL = '/nope/does/not/exist/vp'
    expect(resolveViteplusTool(mkWorkspace({ devDependencies: { 'vite-plus': '^1' } }))).toBeNull()
  })
})
