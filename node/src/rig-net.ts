// rig-net — the force-.NET escape hatch. Hands off to the .NET rig tool from
// anywhere (even inside a Node directory). Shipped by the Node package because
// you only need this differentiator when both tools are installed — and if you
// have both, you have this package.
import { spawnSync } from 'node:child_process'
import { findDotnetTool } from './delegate.js'

const tool = findDotnetTool()
if (!tool) {
  process.stderr.write('rig-net: the .NET rig is not installed.\n  dotnet tool install --global rig\n')
  process.exit(1)
}

// RIG_NO_DELEGATE so the .NET tool runs natively (it won't bounce back to Node).
const result = spawnSync(tool, process.argv.slice(2), {
  stdio: 'inherit',
  env: { ...process.env, RIG_NO_DELEGATE: '1' },
})
process.exit(result.status ?? (result.signal ? 1 : 0))
