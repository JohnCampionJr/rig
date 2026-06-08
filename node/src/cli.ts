// The `rig` entry — delegate-aware (hands off to the .NET tool in .NET dirs).
import { run } from './app.js'
import { ui } from './ui.js'

run(true, 'rig').catch((err: unknown) => {
  ui.error(err instanceof Error ? err.message : String(err))
  process.exit(1)
})
