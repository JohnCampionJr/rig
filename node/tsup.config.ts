import { readFileSync } from 'node:fs'
import { defineConfig } from 'tsup'

const { version } = JSON.parse(
  readFileSync(new URL('./package.json', import.meta.url), 'utf8'),
) as { version: string }

export default defineConfig({
  entry: ['src/cli.ts', 'src/rignode.ts', 'src/rigdotnet.ts'],
  format: ['esm'],
  target: 'node18',
  clean: true,
  sourcemap: true,
  // Bake the version in so it survives `bun build --compile` (no package.json
  // at runtime in the standalone binary).
  define: { __RIG_VERSION__: JSON.stringify(version) },
  // Deps stay external; this is a CLI installed with its node_modules.
  banner: { js: '#!/usr/bin/env node' },
})
