#!/usr/bin/env bun
/**
 * Build & install `rigdev` — a standalone dev build of the rig CLI compiled from
 * the working tree with `bun build --compile`. It lets you run your *local*
 * changes as `rigdev` (delegate-aware, exactly like `rig`) without publishing or
 * shadowing the globally installed `rig`.
 *
 * The binary is a snapshot of the source at build time, so re-run this after
 * editing to refresh it (the compile is ~150ms). Requires Bun.
 *
 *   bun run install:rigdev                  # → ~/.local/bin/rigdev
 *   RIG_DEV_BIN=/some/dir bun run install:rigdev
 *
 * Make sure the target dir is on your PATH, then `rigdev` runs your working tree.
 */
import { spawnSync } from 'node:child_process'
import { chmodSync, mkdirSync } from 'node:fs'
import { homedir, platform } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const scriptDir = dirname(fileURLToPath(import.meta.url)) // node/scripts
const entry = join(scriptDir, '..', 'src', 'cli.ts') // node/src/cli.ts — derived, never hardcoded
const isWin = platform() === 'win32'

const binDir = process.env.RIG_DEV_BIN ?? join(homedir(), '.local', 'bin')
const out = join(binDir, isWin ? 'rigdev.exe' : 'rigdev')

mkdirSync(binDir, { recursive: true })
console.log(`Building rigdev from ${entry}`)

const res = spawnSync('bun', ['build', entry, '--compile', '--outfile', out], { stdio: 'inherit' })
if (res.status !== 0) {
  console.error('\n✗ rigdev build failed (is Bun installed?).')
  process.exit(res.status ?? 1)
}
if (!isWin) chmodSync(out, 0o755)

console.log(`\n✓ rigdev installed → ${out}`)
console.log(`  Ensure ${binDir} is on your PATH, then run \`rigdev\` to exercise your working tree.`)
console.log('  Re-run `bun run install:rigdev` after editing source to refresh the binary.')
