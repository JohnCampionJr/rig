import { cli } from 'gunshi'
import completion from '@gunshi/plugin-completion'
import { ALIASES, buildSubCommands, rootCommand, verbNames } from './commands.js'
import { maybeDelegate } from './delegate.js'
import { setDryRun } from './exec.js'
import { peekGlobalFlags, preparse } from './preparse.js'
import { loadSession } from './session.js'
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

  // As `rig`, hand off to the .NET tool when this is a .NET project. As
  // `rignode`, skip this entirely.
  if (delegate) maybeDelegate(rawArgv)

  // Global flags are needed before the session (env depends on --no-env).
  const flags = peekGlobalFlags(rawArgv)
  setDryRun(flags.dryRun)

  const session = await loadSession(flags)
  ui.setQuiet(flags.quiet || session.config.quiet === true)

  // Expand the leading watch modifier, verb aliases, and unambiguous prefixes.
  const argv = preparse(rawArgv, verbNames(session), ALIASES)

  await cli(argv, rootCommand(session), {
    name: process.env.RIG_BIN_NAME || defaultName,
    version: VERSION,
    description: 'A convention-first Node dev launcher — less typing, menu-driven, workspace-aware.',
    subCommands: buildSubCommands(session),
    renderHeader: null,
    plugins: [completion()],
  })
}
