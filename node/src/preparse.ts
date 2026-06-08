import type { Flags } from './types.js'

/**
 * Pre-parse argv rewriting done before gunshi sees it — the bits a conventional
 * parser fights: the leading `watch` modifier and unambiguous verb-prefix
 * expansion. All pure and unit-tested.
 */

const WATCH_TOKENS = new Set(['watch', 'w'])

/**
 * Peek the recursive global flags from raw argv (they may appear anywhere).
 * gunshi also parses them via the global plugin; we read them early because the
 * session env depends on `--no-env`.
 */
export function peekGlobalFlags(argv: string[]): Flags {
  const has = (...names: string[]) => argv.some((a) => names.includes(a))
  return {
    dryRun: has('--dry-run', '-n'),
    quiet: has('--quiet', '-q'),
    noEnv: has('--no-env'),
  }
}

/**
 * Expand a leading `watch`/`w` modifier: `watch dev` → `dev --watch`,
 * `w t` → `t --watch`. Bare `watch` → `[]` (falls through to the menu).
 */
export function expandWatch(args: string[]): string[] {
  if (args.length === 0) return args
  const first = args[0]!
  if (!WATCH_TOKENS.has(first)) return args
  const rest = args.slice(1)
  if (rest.length === 0) return []
  return [...rest, '--watch']
}

/**
 * Expand a short verb alias in the command position (`t` → `test`, `d` → `dev`).
 * Aliases are resolved here rather than registered as gunshi sub-commands so
 * `--help` stays free of duplicate rows. Deterministic single letters that
 * prefix-matching couldn't disambiguate live here.
 */
export function expandAlias(args: string[], aliases: Record<string, string>): string[] {
  if (args.length === 0) return args
  const token = args[0]!
  const full = aliases[token]
  return full ? [full, ...args.slice(1)] : args
}

/**
 * Expand an unambiguous verb prefix in the command position: `cove` →
 * `coverage`, `reb` → `rebuild`. Exact names and option-like tokens pass
 * through unchanged; ambiguous or unknown tokens pass through (so script verbs
 * and fuzzy errors are handled downstream).
 */
export function expandPrefix(args: string[], verbNames: string[]): string[] {
  if (args.length === 0) return args
  const token = args[0]!
  if (token.startsWith('-')) return args
  if (verbNames.includes(token)) return args

  const matches = verbNames.filter((name) => name.startsWith(token))
  if (matches.length === 1) return [matches[0]!, ...args.slice(1)]
  return args
}

/** Apply watch, then alias, then prefix expansion to the command position. */
export function preparse(
  argv: string[],
  verbNames: string[],
  aliases: Record<string, string> = {},
): string[] {
  return expandPrefix(expandAlias(expandWatch(argv), aliases), verbNames)
}
