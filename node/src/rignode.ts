// The `rignode` entry — the force-Node escape hatch. Never delegates; always
// runs the Node tool, even inside a .NET directory.
import { run } from './app.js'
import { ui } from './ui.js'

run(false, 'rignode').catch((err: unknown) => {
  ui.error(err instanceof Error ? err.message : String(err))
  process.exit(1)
})
