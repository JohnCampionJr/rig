// Compile dist/cli.js into standalone binaries for every platform via Bun.
// Run after `pnpm build`. Requires Bun on PATH (cross-targets are downloaded
// on first use). Output goes to dist-bin/.
import { execSync } from 'node:child_process'
import { mkdirSync } from 'node:fs'

const targets = [
  ['bun-darwin-arm64', 'rig-darwin-arm64'],
  ['bun-darwin-x64', 'rig-darwin-x64'],
  ['bun-linux-x64', 'rig-linux-x64'],
  ['bun-linux-arm64', 'rig-linux-arm64'],
  ['bun-windows-x64', 'rig-windows-x64.exe'],
]

// Allow `node build-binaries.mjs darwin-arm64 windows-x64` to build a subset.
const only = process.argv.slice(2)
const selected = only.length ? targets.filter(([, out]) => only.some((o) => out.includes(o))) : targets

mkdirSync('dist-bin', { recursive: true })
for (const [target, out] of selected) {
  console.log(`\n▸ building ${out} (${target})`)
  execSync(`bun build ./dist/cli.js --compile --target=${target} --outfile dist-bin/${out}`, {
    stdio: 'inherit',
  })
}
console.log(`\n✓ done — ${selected.length} binary(ies) in dist-bin/`)
