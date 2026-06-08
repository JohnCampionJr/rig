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

/**
 * Spawn a command with an explicit argv (no shell). Echoes, honors --dry-run,
 * and resolves to the child's exit code.
 */
export async function run(file: string, args: string[], opts: RunOptions = {}): Promise<number> {
  ui.command(opts.display ?? displayOf(file, args))
  if (state.dryRun) return 0

  // On Windows, .cmd/.bat shims (e.g. node_modules/.bin/tsc.cmd) must go through
  // the shell; elsewhere spawn the binary directly.
  const useShell = process.platform === 'win32' && /\.(cmd|bat)$/i.test(file)

  return new Promise((resolve) => {
    const child = spawn(file, args, {
      cwd: opts.cwd,
      env: opts.env ? { ...process.env, ...opts.env } : process.env,
      stdio: 'inherit',
      shell: useShell,
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
  const shell = isWindows ? 'cmd' : '/bin/sh'
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
  if (process.platform === 'win32') return run('cmd', ['/c', 'start', '', target], { display: `open ${target}` })
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
