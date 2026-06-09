import { spawnSync } from 'node:child_process'
import { cli } from 'gunshi'
import { aliasesFor, buildSubCommands, invalidVerbMessage, rootCommand, verbNames } from './commands.js'
import { findDotnetTool, maybeDelegate, nearestEcosystem } from './delegate.js'
import { setDryRun } from './exec.js'
import { peekGlobalFlags, preparse } from './preparse.js'
import { loadSession } from './session.js'
import { isSuggestDirective, suggestCompletions } from './suggest.js'
import { ui } from './ui.js'

// Replaced at build time by tsup's `define` (see tsup.config.ts), so the version
// is baked into the bundle / Bun-compiled binary.
declare const __RIG_VERSION__: string | undefined
const VERSION = typeof __RIG_VERSION__ === 'string' ? __RIG_VERSION__ : '0.0.0'

/**
 * The Node tool's entry. `delegate` is true when invoked as `rig` (hand off to
 * the .NET tool in a .NET directory) and false when invoked as `rignode` (the
 * force-Node escape hatch — always run Node). `defaultName` is the program name
 * shown in --help/completion.
 */
export async function run(delegate: boolean, defaultName: string): Promise<void> {
  const rawArgv = process.argv.slice(2)

  // Shell completion (shared `[suggest:N] "<line>"` protocol). Handle before
  // gunshi — which can't parse the directive — and route by ecosystem.
  if (isSuggestDirective(rawArgv[0])) {
    await runSuggest(rawArgv, delegate)
    return
  }

  // As `rig`, hand off to the .NET tool when this is a .NET project. As
  // `rignode`, skip this entirely.
  if (delegate) maybeDelegate(rawArgv)

  // Global flags are needed before the session (env depends on --no-env).
  const flags = peekGlobalFlags(rawArgv)
  setDryRun(flags.dryRun)

  const session = await loadSession(flags)
  ui.setQuiet(flags.quiet || session.config.quiet === true)

  // Expand the leading watch modifier, verb aliases, and unambiguous prefixes.
  const argv = preparse(rawArgv, verbNames(session), aliasesFor(session))

  // Catch an invalid leading verb before gunshi (which would print a terse
  // "Command not found") — a known-but-inapplicable verb gets a why.
  const invalid = invalidVerbMessage(session, argv[0])
  if (invalid) {
    ui.error(invalid)
    process.exitCode = 1
    return
  }

  await cli(argv, rootCommand(session), {
    name: process.env.RIG_BIN_NAME || defaultName,
    version: VERSION,
    description: 'A convention-first Node dev launcher — less typing, menu-driven, workspace-aware.',
    subCommands: buildSubCommands(session),
    renderHeader: null,
  })
}

/**
 * Answer a `[suggest:N] "<line>"` completion request. As `rig` in a .NET
 * project, forward to the .NET tool's native `[suggest]` (same protocol — the
 * output passes straight through). Otherwise answer natively from the Node
 * workspace. Completion must never write noise to stdout, so a missing target
 * or any failure yields an empty list (not a nudge), and stays silent.
 */
async function runSuggest(rawArgv: string[], delegate: boolean): Promise<void> {
  if (delegate && !process.env.RIG_NO_DELEGATE && nearestEcosystem(process.cwd()) === 'dotnet') {
    const tool = findDotnetTool()
    if (tool) {
      const result = spawnSync(tool, rawArgv, {
        stdio: 'inherit',
        env: { ...process.env, RIG_NO_DELEGATE: '1' },
      })
      process.exit(result.status ?? 0)
    }
    process.exit(0) // no .NET tool installed → offer nothing rather than an error
  }

  try {
    const flags = peekGlobalFlags(rawArgv)
    const session = await loadSession(flags)
    const line = rawArgv[1] ?? ''
    const out = suggestCompletions(session, line)
    if (out.length) process.stdout.write(out.join('\n') + '\n')
  } catch {
    // A broken config / discovery error shouldn't spew into the shell.
  }
  process.exit(0)
}
