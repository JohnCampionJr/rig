import { describe, expect, it } from 'vitest'
import { existsSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { run, setDryRun, winCmdInvocation } from '../src/exec.js'

describe('winCmdInvocation (cmd.exe quoting)', () => {
  it('routes through cmd.exe with the program preserved', () => {
    const { file, args } = winCmdInvocation('pnpm.cmd', ['run', 'build'])
    expect(file.toLowerCase()).toMatch(/cmd(\.exe)?$/)
    expect(args.slice(0, 3)).toEqual(['/d', '/s', '/c'])
    expect(args[3]).toContain('pnpm.cmd')
  })

  it('caret-escapes metacharacters so an arg cannot break out', () => {
    const line = winCmdInvocation('x.cmd', ['a & echo pwned > out.txt']).args[3]
    // every shell-significant char is caret-prefixed — no bare `&`, `>`, `|`.
    expect(line).toContain('^&')
    expect(line).toContain('^>')
    expect(line).not.toMatch(/[^^]&[^^]/) // no un-escaped ampersand
  })

  it('escapes embedded double quotes (the breakout vector)', () => {
    const line = winCmdInvocation('x.cmd', ['a" & calc & "b']).args[3]
    expect(line).not.toMatch(/[^\\^]"[^^]/) // every " is backslash- or caret-guarded
  })

  it('routes through the absolute %ComSpec% cmd.exe (no cwd hijack)', () => {
    const prev = process.env.ComSpec
    process.env.ComSpec = 'C:\\Windows\\System32\\cmd.exe'
    try {
      expect(winCmdInvocation('x.cmd', []).file).toBe('C:\\Windows\\System32\\cmd.exe')
    } finally {
      if (prev === undefined) delete process.env.ComSpec
      else process.env.ComSpec = prev
    }
  })
})

// The real proof — only runs on Windows CI, where cmd.exe is the actual parser.
describe.runIf(process.platform === 'win32')('Windows .cmd injection (real spawn)', () => {
  it('passes an adversarial argument literally without executing it', async () => {
    setDryRun(false)
    const dir = mkdtempSync(join(tmpdir(), 'rig-exec-'))
    try {
      // A shim that ignores its args entirely — it only proves it ran.
      const shim = join(dir, 'noop.cmd')
      writeFileSync(shim, '@echo off\r\n> "%~dp0ran.txt" echo ok\r\n')
      const marker = join(dir, 'PWNED.txt')

      // If the quoting were broken, `& echo pwned > PWNED.txt` would run at the
      // cmd level and create the marker.
      await run(shim, [`x & echo pwned > "${marker}"`], { cwd: dir })

      expect(existsSync(join(dir, 'ran.txt'))).toBe(true) // the shim itself ran
      expect(existsSync(marker)).toBe(false) // …and nothing was injected
    } finally {
      rmSync(dir, { recursive: true, force: true })
    }
  })
})
