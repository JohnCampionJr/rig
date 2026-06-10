import { spawnSync } from 'node:child_process'
import { existsSync, readdirSync } from 'node:fs'
import { homedir } from 'node:os'
import { dirname, join } from 'node:path'

const DOTNET_EXTS = ['.sln', '.slnx', '.csproj', '.fsproj']

function isDotnetDir(dir: string): boolean {
  try {
    return readdirSync(dir).some((f) => DOTNET_EXTS.some((ext) => f.endsWith(ext)))
  } catch {
    return false
  }
}

/** Nearest project marker walking up from `start`: 'dotnet' | 'node' | null. */
export function nearestEcosystem(start: string): 'dotnet' | 'node' | null {
  let dir = start
  for (;;) {
    if (isDotnetDir(dir)) return 'dotnet'
    if (existsSync(join(dir, 'package.json'))) return 'node'
    const parent = dirname(dir)
    if (parent === dir) return null
    dir = parent
  }
}

/**
 * Locate the .NET rig tool by its install location — *not* by the name
 * `rig-net` (that's our own bin; looking it up on PATH would loop). The .NET
 * tool installs as `rig` into the dotnet global-tools dir. Override with
 * $RIG_DOTNET_TOOL for non-standard installs.
 */
export function findDotnetTool(): string | null {
  const override = process.env.RIG_DOTNET_TOOL
  if (override) return existsSync(override) ? override : null

  const base = join(homedir(), '.dotnet', 'tools')
  const candidate = process.platform === 'win32' ? join(base, 'rig.exe') : join(base, 'rig')
  return existsSync(candidate) ? candidate : null
}

/**
 * Other-awareness: if the current directory is a .NET project (nearer than any
 * Node project), hand off to the .NET rig. If it isn't installed, print a gentle
 * note. Either way the process exits — the caller stops. No-op (returns) when
 * this is a Node project, so the Node tool runs normally.
 *
 * Shell completion is routed separately (the shared `[suggest]` protocol, see
 * app.ts/runSuggest) — it never reaches here.
 */
export function maybeDelegate(argv: string[], cwd: string = process.cwd()): void {
  // A handoff sets RIG_NO_DELEGATE so the target runs natively (no bounce-back).
  if (process.env.RIG_NO_DELEGATE) return
  if (nearestEcosystem(cwd) !== 'dotnet') return

  const tool = findDotnetTool()
  if (!tool) {
    process.stderr.write(
      "📁 .NET project — the Node rig doesn't handle these.\n" +
        '   Install the .NET rig for `rig` to work here too:\n' +
        '   dotnet tool install --global rig\n',
    )
    process.exit(1)
  }

  const result = spawnSync(tool, argv, {
    stdio: 'inherit',
    env: { ...process.env, RIG_NO_DELEGATE: '1' },
  })
  process.exit(result.status ?? (result.signal ? 1 : 0))
}
