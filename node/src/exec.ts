import { spawn, spawnSync } from 'node:child_process'
import { ui } from './ui.js'

/** Module-level execution flags, set once from parsed global flags. */
const state = { dryRun: false }

export function setDryRun(value: boolean) {
  state.dryRun = value
}

export function isDryRun() {
  return state.dryRun
}

export interface RunOptions {
  cwd?: string
  /** Full env to apply (already merged); when omitted the child inherits ambient. */
  env?: Record<string, string> | undefined
  /** Override the echoed display string (defaults to `file args…`). */
  display?: string
}

/** Quote a token for display only (not used for the actual argv). */
function quoteForDisplay(token: string): string {
  return /\s/.test(token) ? `"${token}"` : token
}

export function displayOf(file: string, args: string[]): string {
  return [file, ...args].map(quoteForDisplay).join(' ')
}

// cmd.exe metacharacters that must be caret-escaped — it parses the command line
// before the target program does. (Ported from cross-spawn, MIT.)
const CMD_META = /([()[\]%!^"`<>&|;, *?])/g

/**
 * Absolute path to cmd.exe (from %ComSpec%). Spawning a bare `cmd.exe` lets
 * Windows search the *current directory* first, so a hostile repo could plant a
 * `cmd.exe` that runs instead of the real one — resolving it to an absolute path
 * closes that.
 */
export function comspec(): string {
  return (
    process.env.ComSpec ||
    process.env.comspec ||
    `${process.env.SystemRoot || 'C:\\Windows'}\\System32\\cmd.exe`
  )
}

/**
 * A safe `cmd.exe` invocation for a Windows `.cmd`/`.bat` shim. Node's
 * `{ shell: true }` joins argv into a command *string* without escaping, so a
 * metacharacter — or a `"` that breaks out of the quoting — in an argument can
 * inject a second command. Here the program and every argument are MSVCRT-quoted
 * then caret-escaped, so args reach the shim verbatim and nothing else runs.
 * Pure — exported for tests.
 */
export function winCmdInvocation(file: string, args: string[]): { file: string; args: string[] } {
  const escArg = (arg: string) => {
    // Quote so the target program's CommandLineToArgvW sees a single argument…
    let s = arg.replace(/(\\*)"/g, '$1$1\\"').replace(/(\\*)$/, '$1$1')
    s = `"${s}"`
    // …then caret-escape twice — once for cmd's parse of the `/c "…"` line, and
    // again because the whole command is wrapped in an outer pair of quotes.
    return s.replace(CMD_META, '^$1').replace(CMD_META, '^$1')
  }
  const line = [file.replace(CMD_META, '^$1'), ...args.map(escArg)].join(' ')
  return { file: comspec(), args: ['/d', '/s', '/c', `"${line}"`] }
}

/**
 * Spawn a command with an explicit argv (no shell). Echoes, honors --dry-run,
 * and resolves to the child's exit code.
 */
export async function run(file: string, args: string[], opts: RunOptions = {}): Promise<number> {
  ui.command(opts.display ?? displayOf(file, args))
  if (state.dryRun) return 0

  // A Windows .cmd/.bat shim (pnpm.cmd, node_modules/.bin/tsc.cmd, …) can't be
  // exec'd directly — it needs cmd.exe. Build a safely-quoted invocation instead
  // of `{ shell: true }`, which would leave the args unescaped and injectable.
  // Everywhere else, spawn the binary directly with no shell at all.
  const viaCmd = process.platform === 'win32' && /\.(cmd|bat)$/i.test(file)
  const inv = viaCmd ? winCmdInvocation(file, args) : { file, args }

  return new Promise((resolve) => {
    const child = spawn(inv.file, inv.args, {
      cwd: opts.cwd,
      env: opts.env ? { ...process.env, ...opts.env } : process.env,
      stdio: 'inherit',
      windowsVerbatimArguments: viaCmd,
    })
    child.on('error', (err) => {
      ui.error(`failed to launch ${file}: ${err.message}`)
      resolve(127)
    })
    child.on('close', (code, signal) => {
      if (signal) resolve(1)
      else resolve(code ?? 0)
    })
  })
}

/**
 * Run a shell command string (used for user-defined string commands, where
 * pipes / && / expansion are expected to work).
 */
export async function runShell(command: string, opts: RunOptions = {}): Promise<number> {
  ui.command(opts.display ?? command)
  if (state.dryRun) return 0

  const isWindows = process.platform === 'win32'
  const shell = isWindows ? comspec() : '/bin/sh'
  const shellArgs = isWindows ? ['/c', command] : ['-c', command]

  return new Promise((resolve) => {
    const child = spawn(shell, shellArgs, {
      cwd: opts.cwd,
      env: opts.env ? { ...process.env, ...opts.env } : process.env,
      stdio: 'inherit',
      shell: false,
    })
    child.on('error', (err) => {
      ui.error(`shell failed: ${err.message}`)
      resolve(127)
    })
    child.on('close', (code, signal) => {
      if (signal) resolve(1)
      else resolve(code ?? 0)
    })
  })
}

/** Open a file/URL with the OS default handler (open / xdg-open / start). */
export function openPath(target: string): Promise<number> {
  if (process.platform === 'darwin') return run('open', [target], { display: `open ${target}` })
  if (process.platform === 'win32')
    return run(comspec(), ['/c', 'start', '', target], { display: `open ${target}` })
  return run('xdg-open', [target], { display: `open ${target}` })
}

/**
 * Capture a command's stdout synchronously (read-only; ignores --dry-run).
 * Returns null on failure.
 */
export function capture(file: string, args: string[], cwd?: string): string | null {
  try {
    const result = spawnSync(file, args, { cwd, encoding: 'utf8', shell: false })
    if (result.status !== 0) return null
    return result.stdout
  } catch {
    return null
  }
}
