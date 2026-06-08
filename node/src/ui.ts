import pc from 'picocolors'

/**
 * Console output helpers. The `→ command` echo and diagnostics go to stderr so
 * a launched command's stdout stays clean for piping.
 */
let quiet = false

export const ui = {
  setQuiet(value: boolean) {
    quiet = value
  },

  /** Echo the command about to run, e.g. `→ pnpm dev`. */
  command(display: string) {
    if (!quiet) process.stderr.write(pc.cyan(`→ ${display}\n`))
  },

  info(message: string) {
    process.stderr.write(`${message}\n`)
  },

  /** A dimmed note. */
  dim(message: string) {
    process.stderr.write(pc.dim(`${message}\n`))
  },

  warn(message: string) {
    process.stderr.write(pc.yellow(`! ${message}\n`))
  },

  error(message: string) {
    process.stderr.write(pc.red(`✗ ${message}\n`))
  },

  success(message: string) {
    process.stderr.write(pc.green(`✓ ${message}\n`))
  },

  /** Plain stdout line (for data output like `info`). */
  out(message: string) {
    process.stdout.write(`${message}\n`)
  },
}

export { pc }
