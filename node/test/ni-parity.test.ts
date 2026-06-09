import { describe, expect, it } from 'vitest'
import { resolveCommand, type Agent } from 'package-manager-detector'
import {
  addCmd,
  executeCmd,
  frozenCmd,
  globalAddCmd,
  installCmd,
  runScriptCmd,
  uninstallCmd,
  upgradeCmd,
} from '../src/pm.js'
import type { PackageManager } from '../src/types.js'

/**
 * Parity with @antfu/ni.
 *
 * antfu extracted ni's command table into `package-manager-detector` (which rig
 * already depends on) and exposes it as `resolveCommand(agent, op, args)` — the
 * exact map ni resolves commands through. These tests treat that as the oracle
 * and assert rig's command builders produce the same commands ni would.
 *
 * Two flavours of assertion:
 *  - `expectExact`     — byte-identical argv. Used for the builders that delegate
 *                        straight to `resolveCommand` (dlx/uninstall/ci/upgrade)
 *                        and for `run`, where arg *order* is semantically load-bearing.
 *  - `expectSemantic`  — equal after normalizing ni's terse npm spellings against
 *                        rig's verbose ones (`i`≡`install`, `-g`≡`--global`,
 *                        `-D`≡`--save-dev`, a redundant `--save` dropped) and
 *                        ignoring arg order. Used for install/add/global, where
 *                        rig deliberately keeps the long forms (they behave
 *                        identically) — see the KNOWN DIVERGENCE notes below.
 */

type Cmd = { file: string; args: string[] }
type Oracle = { command: string; args: string[] }

function oracle(agent: Agent, op: Parameters<typeof resolveCommand>[1], args: string[]): Oracle {
  const r = resolveCommand(agent, op, args)
  if (!r) throw new Error(`oracle has no ${op} for ${agent}`)
  return r
}

function expectExact(rig: Cmd | null, o: Oracle) {
  expect(rig).not.toBeNull()
  expect([rig!.file, ...rig!.args]).toEqual([o.command, ...o.args])
}

/** Collapse ni's terse npm forms to rig's verbose ones; ignore arg order. */
function semanticKey(file: string, args: string[]): string {
  const a = args
    .map((t) => (t === 'i' ? 'install' : t))
    .map((t) => (t === '-g' ? '--global' : t))
    .map((t) => (t === '-D' ? '--save-dev' : t))
    .filter((t) => t !== '--save') // `--save` is npm's default → pure noise
    .sort()
  return [file, ...a].join(' ')
}

function expectSemantic(rig: Cmd, o: Oracle) {
  expect(semanticKey(rig.file, rig.args)).toBe(semanticKey(o.command, o.args))
}

// The four package-manager families rig drives directly (pm-keyed builders).
const PMS: PackageManager[] = ['npm', 'pnpm', 'yarn', 'bun']
// Every agent ni distinguishes, including the yarn classic/Berry split.
const AGENTS: Agent[] = ['npm', 'pnpm', 'yarn', 'yarn@berry', 'bun']

describe('ni parity — install / add (verbose forms, semantic match)', () => {
  it('install ≡ ni install', () => {
    for (const pm of PMS) expectSemantic(installCmd(pm), oracle(pm, 'install', []))
  })

  it('add <pkg> ≡ ni add', () => {
    for (const pm of PMS) expectSemantic(addCmd(pm, 'vite', false), oracle(pm, 'add', ['vite']))
  })

  it('add -D <pkg> ≡ ni add -D', () => {
    for (const pm of PMS) expectSemantic(addCmd(pm, 'vite', true), oracle(pm, 'add', ['vite', '-D']))
  })
})

describe('ni parity — run script (exact, order matters)', () => {
  it('run <script> -- <args> ≡ nr', () => {
    for (const pm of PMS) {
      const rig = runScriptCmd(pm, 'dev', ['--port', '3000'])
      expectExact(rig, oracle(pm, 'run', ['dev', '--port', '3000']))
    }
  })

  it('run <script> with no args ≡ nr', () => {
    for (const pm of PMS) expectExact(runScriptCmd(pm, 'build', []), oracle(pm, 'run', ['build']))
  })
})

describe('ni parity — dlx / uninstall / ci / upgrade (delegated, exact, all agents)', () => {
  it('dlx ≡ nlx for every agent (incl. yarn classic/Berry split)', () => {
    for (const a of AGENTS) expectExact(executeCmd(a, 'cowsay', []), oracle(a, 'execute', ['cowsay']))
  })

  it('dlx forwards trailing args in order', () => {
    for (const a of AGENTS) {
      expectExact(executeCmd(a, 'cowsay', ['moo', '-f', 'tux']), oracle(a, 'execute', ['cowsay', 'moo', '-f', 'tux']))
    }
  })

  it('global add ≡ ni -g for every agent (incl. yarn Berry → npm i -g)', () => {
    for (const a of AGENTS) expectExact(globalAddCmd(a, 'typescript'), oracle(a, 'global', ['typescript']))
  })

  it('uninstall ≡ nun for every agent', () => {
    for (const a of AGENTS) expectExact(uninstallCmd(a, 'vite'), oracle(a, 'uninstall', ['vite']))
  })

  it('ci ≡ nci (frozen install) for every agent', () => {
    for (const a of AGENTS) expectExact(frozenCmd(a), oracle(a, 'frozen', []))
  })

  it('upgrade ≡ nu for every agent', () => {
    for (const a of AGENTS) expectExact(upgradeCmd(a, ['vite']), oracle(a, 'upgrade', ['vite']))
  })

  it('upgrade -i ≡ nu -i where the pm supports it (npm has none)', () => {
    for (const a of AGENTS) {
      const ni = resolveCommand(a, 'upgrade-interactive', ['vite'])
      if (ni) expectExact(upgradeCmd(a, ['vite'], true), ni)
      else expect(upgradeCmd(a, ['vite'], true)).toBeNull() // npm: no interactive upgrade
    }
  })
})

describe('ni parity — fixed divergences are pinned', () => {
  it('bun add -D uses -D (not bun-only -d)', () => {
    expect(addCmd('bun', 'vite', true).args).toEqual(['add', '-D', 'vite'])
  })

  it('npm dlx has no implicit -y (matches bare npx, like ni)', () => {
    expect(executeCmd('npm', 'cowsay', [])).toEqual({ file: 'npx', args: ['cowsay'] })
  })

  it('yarn classic dlx is npx; yarn Berry dlx is `yarn dlx`', () => {
    expect(executeCmd('yarn', 'cowsay', [])).toEqual({ file: 'npx', args: ['cowsay'] })
    expect(executeCmd('yarn@berry', 'cowsay', [])).toEqual({ file: 'yarn', args: ['dlx', 'cowsay'] })
  })
})
