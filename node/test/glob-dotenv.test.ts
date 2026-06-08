import { describe, expect, it } from 'vitest'
import { matchAnyGlob, matchGlob } from '../src/glob.js'
import { parseDotEnv } from '../src/dotenv.js'

describe('glob', () => {
  it('matches * within a segment', () => {
    expect(matchGlob('*-bench', 'app-bench')).toBe(true)
    expect(matchGlob('*-bench', 'app-bench/sub')).toBe(false)
  })
  it('matches ** across segments', () => {
    expect(matchGlob('examples/**', 'examples/a/b')).toBe(true)
  })
  it('is case-insensitive and anchored', () => {
    expect(matchGlob('Demo', 'demo')).toBe(true)
    expect(matchGlob('demo', 'demo-x')).toBe(false)
  })
  it('matchAnyGlob', () => {
    expect(matchAnyGlob(['a*', 'b*'], 'bravo')).toBe(true)
    expect(matchAnyGlob(['a*'], 'bravo')).toBe(false)
  })
})

describe('parseDotEnv', () => {
  it('parses simple pairs and ignores comments/blanks', () => {
    expect(parseDotEnv('# c\nA=1\n\nB=2')).toEqual({ A: '1', B: '2' })
  })
  it('strips export and trims', () => {
    expect(parseDotEnv('export FOO = bar')).toEqual({ FOO: 'bar' })
  })
  it('honors double-quote escapes', () => {
    expect(parseDotEnv('A="line1\\nline2"')).toEqual({ A: 'line1\nline2' })
  })
  it('keeps single-quoted literals', () => {
    expect(parseDotEnv("A='no\\nescape'")).toEqual({ A: 'no\\nescape' })
  })
  it('strips inline comments on unquoted values', () => {
    expect(parseDotEnv('A=bar # trailing')).toEqual({ A: 'bar' })
  })
})
