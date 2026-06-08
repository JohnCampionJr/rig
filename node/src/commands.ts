import { define } from 'gunshi'
import { allScriptNames } from './discovery.js'
import { dispatchDevLoop, dispatchDevLoopAll, dispatchScript } from './dispatch.js'
import { DEV_LOOP_VERBS, applicableDevLoopVerbs, explainUnavailable, getDevLoopVerb } from './resolve.js'
import { runMenu } from './menu.js'
import { info } from './verbs/info.js'
import { init } from './verbs/init.js'
import { kill } from './verbs/kill.js'
import { setDefault } from './verbs/default.js'
import { add, clean, install, outdated, rebuild } from './verbs/maintenance.js'
import { doctor } from './verbs/doctor.js'
import { coverage } from './verbs/coverage.js'
import { setup } from './verbs/setup.js'
import { update } from './verbs/update.js'
import { printCompletion } from './completion.js'
import { ui } from './ui.js'
import type { Session } from './types.js'

/** Recursive global flags declared on every command (behavior applied in cli.ts). */
const globalArgs = {
  // Wording kept identical to the .NET tool — these are behaviorally identical
  // cross-tool flags, so their --help text should read the same in both.
  'dry-run': { type: 'boolean', short: 'n', description: 'Print what would run (or change) without doing it' },
  quiet: { type: 'boolean', short: 'q', description: 'Suppress the → command echo' },
  'no-env': { type: 'boolean', description: 'Do not load .env / .env.local' },
} as const

type GunshiCtx = { positionals: string[]; rest: string[]; values: Record<string, unknown> }

function setCode(code: number) {
  process.exitCode = code
}

/** gunshi boolean args for each configured env preset (`rig test --log`). */
function presetArgs(session: Session): Record<string, { type: 'boolean'; description: string }> {
  const presets = session.config.envPresets ?? {}
  const args: Record<string, { type: 'boolean'; description: string }> = {}
  for (const [name, vars] of Object.entries(presets)) {
    args[name] = { type: 'boolean', description: `env preset: ${Object.keys(vars).join(', ')}` }
  }
  return args
}

/** Merge the env of every preset flag the user set. */
function collectPresetEnv(session: Session, values: Record<string, unknown>): Record<string, string> | undefined {
  const presets = session.config.envPresets ?? {}
  let env: Record<string, string> | undefined
  for (const [name, vars] of Object.entries(presets)) {
    if (values[name]) env = { ...(env ?? {}), ...vars }
  }
  return env
}

/**
 * The package token, if given. gunshi puts the command name at positionals[0]
 * and the user's trailing token at [1]. We don't declare it as a positional arg
 * because gunshi treats declared positionals as mandatory.
 */
function tokenOf(ctx: GunshiCtx): string | undefined {
  return ctx.positionals[1]
}

/**
 * Short verb aliases → primary verb name. Resolved in preparse (not registered
 * as gunshi sub-commands) so `--help` doesn't show duplicate rows.
 */
export const ALIASES: Record<string, string> = {
  d: 'dev',
  r: 'dev',
  run: 'dev', // cross-ecosystem muscle memory: .NET's `run` → Node's `dev`
  b: 'build',
  t: 'test',
  tc: 'typecheck',
  l: 'lint',
  fmt: 'format',
  k: 'kill',
  i: 'info',
  def: 'default',
  od: 'outdated',
  rb: 'rebuild',
  inst: 'install',
  restore: 'install', // cross-ecosystem muscle memory: .NET's `restore` → Node's `install`
  c: 'coverage',
}

function devLoopCommand(session: Session, name: string) {
  // `dev` is long-running, so the workspace-wide graph run is only offered for
  // the batch verbs (build/test/typecheck/lint/format).
  const supportsAll = name !== 'dev'
  const args: Record<string, unknown> = {
    ...globalArgs,
    watch: { type: 'boolean', short: 'w', description: 'watch mode' },
    ...presetArgs(session),
  }
  if (supportsAll) {
    args.all = { type: 'boolean', short: 'a', description: 'run across all workspace packages (dep order)' }
    args.filter = { type: 'string', description: 'limit --all to packages matching a glob or substring' }
  }

  return define({
    name,
    description: `${name} — run the "${name}" script, else the detected tool`,
    args: args as Parameters<typeof define>[0]['args'],
    run: async (ctx: GunshiCtx) => {
      const env = collectPresetEnv(session, ctx.values)
      if (supportsAll && ctx.values.all) {
        setCode(
          await dispatchDevLoopAll(session, name, {
            filter: ctx.values.filter as string | undefined,
            extraArgs: ctx.rest,
            env,
          }),
        )
        return
      }
      setCode(
        await dispatchDevLoop(session, name, {
          token: tokenOf(ctx),
          extraArgs: ctx.rest,
          watch: Boolean(ctx.values.watch),
          env,
        }),
      )
    },
  })
}

function scriptCommand(session: Session, name: string) {
  return define({
    name,
    description: `Run the "${name}" script`,
    args: { ...globalArgs, ...presetArgs(session) },
    run: async (ctx: GunshiCtx) => {
      setCode(
        await dispatchScript(session, name, {
          token: tokenOf(ctx),
          extraArgs: ctx.rest,
          env: collectPresetEnv(session, ctx.values),
        }),
      )
    },
  })
}

function standaloneCommands(session: Session) {
  const infoCmd = define({
    name: 'info',
    description: 'Show what rig discovered for this repo',
    args: { ...globalArgs },
    run: async () => setCode(await info(session)),
  })
  const doctorCmd = define({
    name: 'doctor',
    description: 'Flag environment problems (node, pm, install state)',
    args: { ...globalArgs },
    run: async () => setCode(await doctor(session)),
  })
  const coverageCmd = define({
    name: 'coverage',
    description: 'Run coverage; --open the report, --min gates the line %',
    args: {
      ...globalArgs,
      open: { type: 'boolean', description: 'open the HTML report' },
      min: { type: 'number', description: 'fail below this line %' },
      ...presetArgs(session),
    },
    run: async (ctx: GunshiCtx) =>
      setCode(
        await coverage(session, {
          token: tokenOf(ctx),
          open: Boolean(ctx.values.open),
          min: ctx.values.min as number | undefined,
          extraArgs: ctx.rest,
          env: collectPresetEnv(session, ctx.values),
        }),
      ),
  })
  const initCmd = define({
    name: 'init',
    description: 'Scaffold a .rig.json',
    args: { ...globalArgs },
    run: async () => setCode(await init(session)),
  })
  const setupCmd = define({
    name: 'setup',
    description: 'Interactive walkthrough to set preferences',
    args: { ...globalArgs },
    run: async () => setCode(await setup(session)),
  })
  const updateCmd = define({
    name: 'update',
    description: 'Update rig to the latest published version',
    args: { ...globalArgs, check: { type: 'boolean', description: 'only report, do not update' } },
    run: async (ctx: GunshiCtx) => setCode(await update(session, { check: Boolean(ctx.values.check) })),
  })
  const defaultCmd = define({
    name: 'default',
    description: 'Show or set the default package',
    args: { ...globalArgs },
    run: async (ctx: GunshiCtx) => setCode(await setDefault(session, tokenOf(ctx))),
  })
  const killCmd = define({
    name: 'kill',
    description: 'Stop dev-server processes (by package or --port)',
    args: {
      ...globalArgs,
      port: { type: 'number', multiple: true, description: 'kill whatever listens on this port' },
    },
    run: async (ctx: GunshiCtx) => {
      const ports = (ctx.values.port as number[] | number | undefined) ?? []
      setCode(await kill(session, { token: tokenOf(ctx), ports: Array.isArray(ports) ? ports : [ports] }))
    },
  })
  const completionCmd = define({
    name: 'completion',
    description: 'Print shell-completion setup (zsh/bash/pwsh)',
    args: { ...globalArgs },
    run: async (ctx: GunshiCtx) => setCode(printCompletion(ctx.positionals[1])),
  })
  return {
    info: infoCmd,
    doctor: doctorCmd,
    coverage: coverageCmd,
    init: initCmd,
    setup: setupCmd,
    update: updateCmd,
    default: defaultCmd,
    kill: killCmd,
    completion: completionCmd,
  }
}

function maintenanceCommands(session: Session) {
  const installCmd = define({
    name: 'install',
    description: 'Install dependencies (detected package manager)',
    args: { ...globalArgs },
    run: async () => setCode(await install(session)),
  })
  const outdatedCmd = define({
    name: 'outdated',
    description: 'List dependencies with newer versions',
    args: { ...globalArgs },
    run: async () => setCode(await outdated(session)),
  })
  const cleanCmd = define({
    name: 'clean',
    description: 'Remove build-output dirs (dist/build/.next/… ; not node_modules)',
    args: { ...globalArgs },
    run: async () => setCode(await clean(session)),
  })
  const rebuildCmd = define({
    name: 'rebuild',
    description: 'Clean build outputs, then build',
    args: { ...globalArgs },
    run: async (ctx: GunshiCtx) => setCode(await rebuild(session, tokenOf(ctx))),
  })
  const addCmd = define({
    name: 'add',
    description: 'Add a dependency to a package',
    args: { ...globalArgs, dev: { type: 'boolean', short: 'D', description: 'add as a devDependency' } },
    run: async (ctx: GunshiCtx) =>
      setCode(
        await add(session, ctx.positionals[1], { dev: Boolean(ctx.values.dev), token: ctx.positionals[2] }),
      ),
  })
  return { install: installCmd, outdated: outdatedCmd, clean: cleanCmd, rebuild: rebuildCmd, add: addCmd }
}

/**
 * Every full verb name rig knows about for prefix expansion + completion
 * (aliases handled separately). Dev-loop verbs are filtered to those that
 * actually apply to this workspace; discovered scripts exclude names already
 * owned by a dev-loop verb so the surface matches `buildSubCommands`.
 */
export function verbNames(session: Session): string[] {
  const devLoop = applicableDevLoopVerbs(session).map((v) => v.name)
  const standalone = ['info', 'doctor', 'coverage', 'init', 'setup', 'update', 'default', 'kill', 'completion', 'install', 'outdated', 'clean', 'rebuild', 'add']
  const devLoopScriptNames = new Set(DEV_LOOP_VERBS.flatMap((v) => v.scripts))
  const scripts = allScriptNames(session.workspace).filter((n) => !devLoopScriptNames.has(n))
  return [...new Set([...devLoop, ...standalone, ...scripts])]
}

/**
 * Validate the leading verb token before gunshi sees it (gunshi prints a terse
 * "Command not found" for an unregistered command). Returns a friendly message
 * when the token is invalid — a *why* for a known-but-inapplicable dev-loop verb
 * (`lint` where nothing lints), the generic unknown-verb line otherwise — or
 * null when the token is a real verb/script (or a flag), so the run proceeds.
 */
export function invalidVerbMessage(session: Session, token: string | undefined): string | null {
  if (!token || token.startsWith('-')) return null
  if (verbNames(session).includes(token)) return null
  const verb = getDevLoopVerb(token)
  return verb
    ? explainUnavailable(verb, 'here')
    : `unknown verb "${token}". Run \`rig\` for the menu or \`rig --help\`.`
}

/** Build the gunshi sub-command map: dev-loop verbs, standalone verbs, and scripts. */
export function buildSubCommands(session: Session): Record<string, ReturnType<typeof define>> {
  const sub: Record<string, ReturnType<typeof define>> = {}

  // Only register dev-loop verbs that make sense here (a matching script, or a
  // declared/installed tool) — `rig lint` shouldn't exist where nothing lints.
  for (const v of applicableDevLoopVerbs(session)) {
    sub[v.name] = devLoopCommand(session, v.name)
  }

  Object.assign(sub, standaloneCommands(session))
  Object.assign(sub, maintenanceCommands(session))

  // Aliases are resolved in preparse, not registered here (keeps --help clean).

  // Discovered scripts not already covered by a dev-loop verb become verbs.
  const devLoopScriptNames = new Set(DEV_LOOP_VERBS.flatMap((v) => v.scripts))
  for (const name of allScriptNames(session.workspace)) {
    if (sub[name] || devLoopScriptNames.has(name)) continue
    sub[name] = scriptCommand(session, name)
  }

  return sub
}

/** The root command: bare `rig` opens the interactive menu; an unknown token errs. */
export function rootCommand(session: Session) {
  return define({
    name: 'rig',
    description: 'A convention-first Node dev launcher',
    args: { ...globalArgs },
    run: async (ctx: GunshiCtx) => {
      // Invalid leading tokens are caught in app.ts before gunshi; this is a
      // safety net for any positional that still reaches the entry command.
      const unknown = ctx.positionals[0]
      if (unknown) {
        ui.error(invalidVerbMessage(session, unknown) ?? `unknown verb "${unknown}".`)
        setCode(1)
        return
      }
      setCode(await runMenu(session))
    },
  })
}
