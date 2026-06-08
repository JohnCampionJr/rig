import { describe, expect, it } from 'vitest'
import { mergeConfig } from '../src/config.js'

describe('mergeConfig', () => {
  it('overlay scalars win, empty strings count as unset', () => {
    expect(mergeConfig({ defaultProject: 'a' }, { defaultProject: 'b' }).defaultProject).toBe('b')
    expect(mergeConfig({ defaultProject: 'a' }, { defaultProject: '  ' }).defaultProject).toBe('a')
    expect(mergeConfig({ defaultProject: 'a' }, {}).defaultProject).toBe('a')
  })

  it('dictionaries union with overlay winning per key', () => {
    const merged = mergeConfig(
      { env: { A: '1', B: '1' } },
      { env: { B: '2', C: '3' } },
    )
    expect(merged.env).toEqual({ A: '1', B: '2', C: '3' })
  })

  it('exclude arrays concat-dedupe', () => {
    const merged = mergeConfig({ exclude: ['*-bench'] }, { exclude: ['*-bench', 'examples/*'] })
    expect(merged.exclude).toEqual(['*-bench', 'examples/*'])
  })

  it('coverage merges shallowly', () => {
    const merged = mergeConfig({ coverage: { open: true, min: 80 } }, { coverage: { min: 90 } })
    expect(merged.coverage).toEqual({ open: true, min: 90 })
  })

  it('kill.match unions', () => {
    const merged = mergeConfig({ kill: { match: ['a'] } }, { kill: { match: ['b'] } })
    expect(merged.kill?.match).toEqual(['a', 'b'])
  })

  it('quiet boolean overrides', () => {
    expect(mergeConfig({ quiet: true }, { quiet: false }).quiet).toBe(false)
    expect(mergeConfig({ quiet: true }, {}).quiet).toBe(true)
  })
})
