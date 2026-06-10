#!/usr/bin/env bun
/**
 * Build & install `rig-dev` — a standalone dev build of the rig CLI compiled from
 * the working tree with `bun build --compile`. It lets you run your *local*
 * changes as `rig-dev` (delegate-aware, exactly like `rig`) without publishing or
 * shadowing the globally installed `rig`.
 *
 * The binary is a snapshot of the source at build time, so re-run this after
 * editing to refresh it (the compile is ~150ms). Requires Bun.
 *
 *   bun run install:rig-dev                  # → ~/.local/bin/rig-dev
 *   RIG_DEV_BIN=/some/dir bun run install:rig-dev
 *
 * Make sure the target dir is on your PATH, then `rig-dev` runs your working tree.
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
const out = join(binDir, isWin ? 'rig-dev.exe' : 'rig-dev')

mkdirSync(binDir, { recursive: true })
console.log(`Building rig-dev from ${entry}`)

const res = spawnSync('bun', ['build', entry, '--compile', '--outfile', out], { stdio: 'inherit' })
if (res.status !== 0) {
  console.error('\n✗ rig-dev build failed (is Bun installed?).')
  process.exit(res.status ?? 1)
}
if (!isWin) chmodSync(out, 0o755)

console.log(`\n✓ rig-dev installed → ${out}`)
console.log(`  Ensure ${binDir} is on your PATH, then run \`rig-dev\` to exercise your working tree.`)
console.log('  Re-run `bun run install:rig-dev` after editing source to refresh the binary.')
