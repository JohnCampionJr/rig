/**
 * Tolerant JSON reader: strips `//` and block comments and trailing commas
 * before parsing, so a hand-edited .rig.json doesn't break loading.
 * String contents are preserved (comment-looking text inside strings is kept).
 */
export function parseJsonc<T = unknown>(text: string): T {
  const withoutComments = text.replace(
    /"(?:\\.|[^"\\])*"|(\/\/[^\n\r]*|\/\*[\s\S]*?\*\/)/g,
    (match, comment) => (comment ? '' : match),
  )
  const withoutTrailingCommas = withoutComments.replace(/,(\s*[}\]])/g, '$1')
  return JSON.parse(withoutTrailingCommas) as T
}

/** Advance past whitespace and `//` / block comments. */
function skipTrivia(text: string, i: number): number {
  for (;;) {
    const c = text[i]
    if (c === ' ' || c === '\t' || c === '\n' || c === '\r') {
      i++
    } else if (c === '/' && text[i + 1] === '/') {
      i += 2
      while (i < text.length && text[i] !== '\n') i++
    } else if (c === '/' && text[i + 1] === '*') {
      i += 2
      while (i < text.length && !(text[i] === '*' && text[i + 1] === '/')) i++
      i += 2
    } else {
      return i
    }
  }
}

/** `i` is at the opening quote; return the index just past the closing quote. */
function scanString(text: string, i: number): number {
  i++
  while (i < text.length) {
    if (text[i] === '\\') {
      i += 2
      continue
    }
    if (text[i] === '"') return i + 1
    i++
  }
  return i
}

/** `i` is at the first char of a JSON value; return the index just past it. */
function scanValue(text: string, i: number): number {
  const c = text[i]
  if (c === '"') return scanString(text, i)
  if (c === '{' || c === '[') {
    const open = c
    const close = c === '{' ? '}' : ']'
    let depth = 0
    while (i < text.length) {
      const ch = text[i]
      if (ch === '"') {
        i = scanString(text, i)
        continue
      }
      if (ch === '/' && (text[i + 1] === '/' || text[i + 1] === '*')) {
        i = skipTrivia(text, i)
        continue
      }
      if (ch === open) depth++
      else if (ch === close) {
        depth--
        if (depth === 0) return i + 1
      }
      i++
    }
    return i
  }
  while (i < text.length && !',}]'.includes(text[i]!) && !' \t\n\r'.includes(text[i]!)) i++
  return i
}

function rootBrace(text: string): number {
  const i = skipTrivia(text, 0)
  return text[i] === '{' ? i : -1
}

/** Find the [start,end) span of a top-level key's value, or null. */
function findTopLevelValue(text: string, key: string, rootStart: number): { start: number; end: number } | null {
  let i = rootStart + 1
  for (;;) {
    i = skipTrivia(text, i)
    const c = text[i]
    if (i >= text.length || c === '}') return null
    if (c === ',') {
      i++
      continue
    }
    if (c !== '"') return null // malformed; bail to a fresh rewrite
    const keyEnd = scanString(text, i)
    let keyName: string
    try {
      keyName = JSON.parse(text.slice(i, keyEnd)) as string
    } catch {
      return null
    }
    i = skipTrivia(text, keyEnd)
    if (text[i] !== ':') return null
    i = skipTrivia(text, i + 1)
    const valueStart = i
    const valueEnd = scanValue(text, i)
    if (keyName === key) return { start: valueStart, end: valueEnd }
    i = valueEnd
  }
}

function detectIndent(text: string, rootStart: number): string {
  const nl = text.indexOf('\n', rootStart)
  if (nl >= 0) {
    let j = nl + 1
    let indent = ''
    while (text[j] === ' ' || text[j] === '\t') {
      indent += text[j]
      j++
    }
    if (indent && text[j] !== '\n' && text[j] !== '\r' && j < text.length) return indent
  }
  return '  '
}

/**
 * Set a top-level key in a JSONC document, splicing the value in place so all
 * comments and formatting are preserved (the .NET JsoncEditor approach).
 * Returns the edited text, or null if the document has no root object (caller
 * should write a fresh file). Only top-level keys are supported.
 */
export function editJsonc(text: string, key: string, value: unknown): string | null {
  const rootStart = rootBrace(text)
  if (rootStart < 0) return null

  const valueStr = JSON.stringify(value)
  const existing = findTopLevelValue(text, key, rootStart)
  if (existing) {
    return text.slice(0, existing.start) + valueStr + text.slice(existing.end)
  }

  // Insert as the first property. A trailing comma is added when others follow.
  const afterBrace = rootStart + 1
  const firstContent = skipTrivia(text, afterBrace)
  const indent = detectIndent(text, rootStart)
  const entry = `${JSON.stringify(key)}: ${valueStr}`
  if (text[firstContent] === '}') {
    return `${text.slice(0, afterBrace)}\n${indent}${entry}\n${text.slice(firstContent)}`
  }
  return `${text.slice(0, afterBrace)}\n${indent}${entry},${text.slice(afterBrace)}`
}
