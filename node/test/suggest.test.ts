import { describe, expect, it } from 'vitest'
import { isSuggestDirective, suggestCompletions } from '../src/suggest.js'
import type { Session } from '../src/types.js'

// suggestCompletions only reads session.workspace.packages (names + scripts);
// a minimal stand-in is enough to exercise the verb/argument-position logic.
function fakeSession(packages: { name: string; scripts?: Record<string, string> }[]): Session {
  return {
    workspace: {
      packages: packages.map((p) => ({ name: p.name, scripts: p.scripts ?? {} })),
    },
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
    { name: 'web', scripts: { dev: 'vite', deploy: 'sh deploy' } },
    { name: 'api' },
  ])

  it('offers verbs + aliases at the verb position', () => {
    const out = suggestCompletions(session, 'rig ')
    expect(out).toContain('build') // known dev-loop verb
    expect(out).toContain('deploy') // discovered script as a verb
    expect(out).toContain('b') // short alias
    expect(out).toContain('completion')
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
