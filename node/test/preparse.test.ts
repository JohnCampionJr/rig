import { describe, expect, it } from 'vitest'
import { ALIASES as REAL_ALIASES } from '../src/commands.js'
import { expandAlias, expandPrefix, expandWatch, peekGlobalFlags, preparse } from '../src/preparse.js'

const VERBS = ['dev', 'build', 'test', 'typecheck', 'lint', 'format', 'coverage', 'kill', 'info']

describe('expandWatch', () => {
  it('rewrites `watch dev` → `dev --watch`', () => {
    expect(expandWatch(['watch', 'dev'])).toEqual(['dev', '--watch'])
  })
  it('rewrites the `w` alias with extra args', () => {
    expect(expandWatch(['w', 't', 'core'])).toEqual(['t', 'core', '--watch'])
  })
  it('bare watch falls through to the menu', () => {
    expect(expandWatch(['watch'])).toEqual([])
  })
  it('leaves non-watch args untouched', () => {
    expect(expandWatch(['build', 'web'])).toEqual(['build', 'web'])
  })
})

describe('expandPrefix', () => {
  it('expands an unambiguous prefix', () => {
    expect(expandPrefix(['typ'], VERBS)).toEqual(['typecheck'])
    expect(expandPrefix(['cov'], VERBS)).toEqual(['coverage'])
  })
  it('keeps exact names', () => {
    expect(expandPrefix(['test'], VERBS)).toEqual(['test'])
  })
  it('does not expand an ambiguous prefix', () => {
    // both nothing here is ambiguous on single letters that match many; 't' → test/typecheck
    expect(expandPrefix(['t'], VERBS)).toEqual(['t'])
  })
  it('passes option-like tokens through', () => {
    expect(expandPrefix(['-q', 'test'], VERBS)).toEqual(['-q', 'test'])
  })
  it('passes unknown tokens through (script verbs)', () => {
    expect(expandPrefix(['seed'], VERBS)).toEqual(['seed'])
  })
  it('preserves trailing args', () => {
    expect(expandPrefix(['cov', 'web'], VERBS)).toEqual(['coverage', 'web'])
  })
})

const ALIASES = { t: 'test', tc: 'typecheck', d: 'dev', b: 'build' }

describe('expandAlias', () => {
  it('expands a known alias in the command position', () => {
    expect(expandAlias(['t', 'web'], ALIASES)).toEqual(['test', 'web'])
    expect(expandAlias(['tc'], ALIASES)).toEqual(['typecheck'])
  })
  it('leaves non-aliases untouched', () => {
    expect(expandAlias(['lint'], ALIASES)).toEqual(['lint'])
  })
  it('maps the .NET idiom verbs onto their Node equivalents', () => {
    // Cross-ecosystem muscle memory: `rig run`/`rig restore` work in the Node tool.
    expect(expandAlias(['run', 'web'], REAL_ALIASES)).toEqual(['dev', 'web'])
    expect(expandAlias(['restore'], REAL_ALIASES)).toEqual(['install'])
  })
})

describe('preparse', () => {
  it('applies watch then prefix expansion', () => {
    expect(preparse(['watch', 'dev'], VERBS)).toEqual(['dev', '--watch'])
    expect(preparse(['w', 'typ'], VERBS)).toEqual(['typecheck', '--watch'])
  })
  it('applies aliases (watch → alias → prefix)', () => {
    expect(preparse(['w', 't', 'web'], VERBS, ALIASES)).toEqual(['test', 'web', '--watch'])
    expect(preparse(['tc'], VERBS, ALIASES)).toEqual(['typecheck'])
  })
})

describe('peekGlobalFlags', () => {
  it('detects flags anywhere in argv', () => {
    expect(peekGlobalFlags(['test', '-q', '--dry-run'])).toEqual({
      dryRun: true,
      quiet: true,
      noEnv: false,
    })
  })
  it('detects --no-env and -n', () => {
    expect(peekGlobalFlags(['build', '-n', '--no-env'])).toEqual({
      dryRun: true,
      quiet: false,
      noEnv: true,
    })
  })
})
