/**
 * Minimal glob → RegExp for `exclude` matching against package names and
 * relative paths. Supports `*`, `?`, and `**`. Case-insensitive, anchored.
 */
export function globToRegExp(pattern: string): RegExp {
  let re = '^'
  for (let i = 0; i < pattern.length; i++) {
    const ch = pattern[i]!
    if (ch === '*') {
      if (pattern[i + 1] === '*') {
        re += '.*'
        i++
        if (pattern[i + 1] === '/') i++ // consume the slash after **
      } else {
        re += '[^/]*'
      }
    } else if (ch === '?') {
      re += '[^/]'
    } else if ('.+^${}()|[]\\'.includes(ch)) {
      re += `\\${ch}`
    } else {
      re += ch
    }
  }
  re += '$'
  return new RegExp(re, 'i')
}

/** True if `value` matches the glob `pattern`. */
export function matchGlob(pattern: string, value: string): boolean {
  return globToRegExp(pattern).test(value)
}

/** True if `value` matches any of the patterns. */
export function matchAnyGlob(patterns: string[], value: string): boolean {
  return patterns.some((p) => matchGlob(p, value))
}
