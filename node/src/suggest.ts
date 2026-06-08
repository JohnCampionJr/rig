import { ALIASES, verbNames } from './commands.js'
import type { Session } from './types.js'

/**
 * Shell completion via the shared `[suggest:N] "<line>"` protocol — the same
 * contract the .NET rig speaks, so one completion script drives both tools and
 * a request can be forwarded across ecosystems with no translation (see
 * app.ts / delegate.ts). The handler emits a plain newline list; the shell
 * filters by the current word (we also prefix-filter so non-filtering shells
 * like PowerShell behave).
 */

const GLOBAL_FLAGS = ['--dry-run', '--quiet', '--no-env']

/** True if `arg` is the `[suggest]` / `[suggest:N]` completion directive. */
export function isSuggestDirective(arg: string | undefined): boolean {
  return typeof arg === 'string' && /^\[suggest(:\d+)?\]$/.test(arg)
}

/**
 * Candidates for a command line. `line` is the whole line the shell is
 * completing (e.g. `rig bu` or `rig build co`); the leading program name is
 * ignored. Verb position offers verbs + aliases; argument position offers
 * workspace package names. Global flags surface once the word starts with `-`.
 */
export function suggestCompletions(session: Session, line: string): string[] {
  const endsWithSpace = /\s$/.test(line)
  const tokens = line.trim().split(/\s+/).filter(Boolean)
  // The word being edited, and how many tokens (incl. the program name) precede it.
  const editing = endsWithSpace ? '' : (tokens[tokens.length - 1] ?? '')
  const completeCount = endsWithSpace ? tokens.length : tokens.length - 1

  let candidates: string[]
  if (completeCount <= 1) {
    // Verb position: available verb names + the aliases that point at them.
    const names = verbNames(session)
    const aliases = Object.keys(ALIASES).filter((a) => names.includes(ALIASES[a]!))
    candidates = [...names, ...aliases]
  } else {
    // Argument position: a workspace package — offer full and short names.
    candidates = session.workspace.packages.flatMap((p) => {
      const short = p.name.includes('/') ? p.name.slice(p.name.lastIndexOf('/') + 1) : p.name
      return short === p.name ? [p.name] : [p.name, short]
    })
  }
  if (editing.startsWith('-')) candidates = [...candidates, ...GLOBAL_FLAGS]

  const uniq = [...new Set(candidates)]
  const filtered = editing ? uniq.filter((c) => c.startsWith(editing)) : uniq
  return filtered.sort()
}
