import { describe, expect, it } from 'vitest'
import { editJsonc, parseJsonc } from '../src/jsonc.js'

describe('parseJsonc', () => {
  it('parses plain JSON', () => {
    expect(parseJsonc('{"a":1}')).toEqual({ a: 1 })
  })
  it('strips line and block comments', () => {
    const text = `{
      // the default package
      "defaultProject": "web", /* inline */
      "quiet": false
    }`
    expect(parseJsonc(text)).toEqual({ defaultProject: 'web', quiet: false })
  })
  it('tolerates trailing commas', () => {
    expect(parseJsonc('{"a":1,"b":2,}')).toEqual({ a: 1, b: 2 })
  })
  it('keeps comment-looking text inside strings', () => {
    expect(parseJsonc('{"url":"https://example.com//x"}')).toEqual({ url: 'https://example.com//x' })
  })
})

describe('editJsonc', () => {
  it('replaces an existing top-level value, preserving comments', () => {
    const text = `{
  // pick the app
  "defaultProject": "web", // current
  "quiet": false
}
`
    const out = editJsonc(text, 'defaultProject', 'api')!
    expect(out).toContain('"defaultProject": "api"')
    expect(out).toContain('// pick the app')
    expect(out).toContain('// current')
    expect(parseJsonc(out)).toEqual({ defaultProject: 'api', quiet: false })
  })

  it('inserts a new key into a non-empty object (keeps existing)', () => {
    const text = `{
  "quiet": true
}
`
    const out = editJsonc(text, 'defaultProject', 'web')!
    expect(parseJsonc(out)).toEqual({ quiet: true, defaultProject: 'web' })
  })

  it('inserts into an empty object', () => {
    const out = editJsonc('{}\n', 'defaultProject', 'web')!
    expect(parseJsonc(out)).toEqual({ defaultProject: 'web' })
  })

  it('returns null when there is no root object', () => {
    expect(editJsonc('', 'a', 1)).toBeNull()
    expect(editJsonc('   \n', 'a', 1)).toBeNull()
  })

  it('does not confuse a key name appearing inside a string value', () => {
    const text = `{
  "note": "set defaultProject here",
  "defaultProject": "web"
}
`
    const out = editJsonc(text, 'defaultProject', 'api')!
    expect(parseJsonc(out)).toEqual({ note: 'set defaultProject here', defaultProject: 'api' })
  })
})
