import { ui } from './ui.js'
import { allScriptNames } from './discovery.js'
import { runDevLoopForPackage, runScriptForPackage } from './dispatch.js'
import { DEV_LOOP_VERBS, applicableDevLoopVerbs, candidatePackages, describeDevLoop, getDevLoopVerb } from './resolve.js'
import { BACK, isInteractive, pickPackage, selectFrom, prompt } from './prompts.js'
import { info } from './verbs/info.js'
import { init } from './verbs/init.js'
import { kill } from './verbs/kill.js'
import { setDefault } from './verbs/default.js'
import { clean, install, outdated, rebuild } from './verbs/maintenance.js'
import { doctor } from './verbs/doctor.js'
import { canCover, coverage } from './verbs/coverage.js'
import { setup } from './verbs/setup.js'
import { update } from './verbs/update.js'
import type { PackageInfo, Session } from './types.js'

/** Script names already covered by dev-loop verbs (kept out of the Scripts group). */
const DEV_LOOP_SCRIPT_NAMES = new Set(DEV_LOOP_VERBS.flatMap((v) => v.scripts))

function extraScripts(session: Session): string[] {
  return allScriptNames(session.workspace).filter((name) => !DEV_LOOP_SCRIPT_NAMES.has(name))
}

/** A package's own scripts, excluding the ones already covered by dev-loop verbs. */
function packageExtraScripts(pkg: PackageInfo): string[] {
  return Object.keys(pkg.scripts)
    .filter((name) => !DEV_LOOP_SCRIPT_NAMES.has(name))
    .sort()
}

function verbHint(session: Session, verbName: string): string | undefined {
  const verb = getDevLoopVerb(verbName)!
  const candidates = candidatePackages(session, verb)
  if (candidates.length === 1) return describeDevLoop(session, verb, candidates[0]!) ?? undefined
  return `${candidates.length} packages`
}

/** Dev-loop verbs worth running under watch, that have at least one target package. */
const WATCHABLE = ['dev', 'build', 'test']
function watchableVerbs(session: Session) {
  return WATCHABLE.map((n) => getDevLoopVerb(n)!).filter((v) => candidatePackages(session, v).length > 0)
}

/**
 * Choose the package a verb/script runs in. A sole candidate is returned
 * directly (no prompt); otherwise a back-aware picker is shown. Returns BACK
 * when the user backs out.
 */
async function choosePackage(
  session: Session,
  candidates: PackageInfo[],
  action: string,
): Promise<PackageInfo | typeof BACK> {
  if (candidates.length === 1) return candidates[0]!
  const picked = await pickPackage(candidates, session.config.defaultProject, `Which package to ${action}?`, {
    back: true,
    current: session.currentPackage,
  })
  return picked && picked !== BACK ? picked : BACK
}

type Choice =
  | { kind: 'verb'; name: string }
  | { kind: 'coverage' }
  | { kind: 'kill' }
  | { kind: 'watch' }
  | { kind: 'scripts' }
  | { kind: 'maintenance' }
  | { kind: 'config' }
  | { kind: 'quit' }

/**
 * The bare-`rig` interactive menu. The *selection* phase loops (so `← back`
 * and Esc move up a level); the moment a real verb/script is chosen it runs
 * once and returns. Returns a process exit code.
 */
export async function runMenu(session: Session): Promise<number> {
  if (!isInteractive()) {
    ui.info('rig — run a verb (or `rig --help`). Interactive menu needs a TTY.')
    ui.info(`verbs: ${DEV_LOOP_VERBS.map((v) => v.name).join(', ')}, kill, info, default, init`)
    return 0
  }

  const available = applicableDevLoopVerbs(session)
  const scripts = extraScripts(session)

  const options: Array<{ value: Choice; label: string; hint?: string }> = []
  for (const v of available) {
    options.push({ value: { kind: 'verb', name: v.name }, label: v.name, hint: verbHint(session, v.name) })
  }
  if (session.workspace.packages.some((p) => canCover(session, p))) {
    options.push({ value: { kind: 'coverage' }, label: 'coverage', hint: 'tests + coverage' })
  }
  options.push({ value: { kind: 'kill' }, label: 'kill', hint: 'stop dev servers' })
  if (watchableVerbs(session).length) {
    options.push({ value: { kind: 'watch' }, label: 'watch ▸', hint: 'dev/build/test, re-run on change' })
  }
  if (scripts.length) {
    options.push({ value: { kind: 'scripts' }, label: 'scripts ▸', hint: `${scripts.length} scripts` })
  }
  options.push({ value: { kind: 'maintenance' }, label: 'maintenance ▸', hint: 'install · outdated · clean · rebuild' })
  options.push({ value: { kind: 'config' }, label: 'config ▸' })
  options.push({ value: { kind: 'quit' }, label: 'quit' })

  prompt.intro('rig')

  for (;;) {
    const choice = await selectFrom<Choice>('What do you want to run?', options)
    if (!choice || choice.kind === 'quit') {
      prompt.outro('bye')
      return 0
    }

    switch (choice.kind) {
      case 'verb': {
        const verb = getDevLoopVerb(choice.name)!
        const pkg = await choosePackage(session, candidatePackages(session, verb), choice.name)
        if (pkg === BACK) continue
        return runDevLoopForPackage(session, verb, pkg)
      }
      case 'coverage': {
        const candidates = session.workspace.packages.filter((p) => canCover(session, p))
        const pkg = await choosePackage(session, candidates, 'coverage')
        if (pkg === BACK) continue
        return coverage(session, { token: pkg.name })
      }
      case 'kill':
        return kill(session)
      case 'watch': {
        const result = await watchMenu(session)
        if (result === BACK) continue
        return result
      }
      case 'scripts': {
        const result = await scriptsMenu(session)
        if (result === BACK) continue
        return result
      }
      case 'maintenance': {
        const result = await maintenanceMenu(session)
        if (result === BACK) continue
        return result
      }
      case 'config': {
        const result = await configMenu(session)
        if (result === BACK) continue
        return result
      }
    }
  }
}

/**
 * Scripts group: pick the package first, then one of its scripts. Single-package
 * repos skip the package step. Backing out of the script list returns to the
 * package picker (or to the top menu when there's only one package).
 */
async function scriptsMenu(session: Session): Promise<number | typeof BACK> {
  const packages = session.workspace.packages.filter((p) => packageExtraScripts(p).length > 0)
  const single = packages.length === 1

  for (;;) {
    let pkg: PackageInfo
    if (single) {
      pkg = packages[0]!
    } else {
      const picked = await pickPackage(packages, session.config.defaultProject, 'Scripts — which package?', {
        back: true,
        current: session.currentPackage,
      })
      if (!picked || picked === BACK) return BACK
      pkg = picked
    }

    const names = packageExtraScripts(pkg)
    const choice = await selectFrom<string | typeof BACK>(`Which script? (${pkg.name})`, [
      ...names.map((name) => ({ value: name, label: name, hint: pkg.scripts[name] })),
      { value: BACK, label: '← back' },
    ])
    if (!choice || choice === BACK) {
      if (single) return BACK // nowhere to go but the top menu
      continue // back to the package picker
    }
    return runScriptForPackage(session, choice, pkg)
  }
}

/**
 * Watch group: pick a dev/build/test verb, then a package, and run it under
 * `--watch`. Mirrors the .NET rig's `watch ▸` submenu (the `w`/`watch` prefix
 * and `-w` flag still work too). Backing out of the verb list returns to the
 * top menu; backing out of the package picker returns to the verb list.
 */
async function watchMenu(session: Session): Promise<number | typeof BACK> {
  const verbs = watchableVerbs(session)
  for (;;) {
    const choice = await selectFrom<string | typeof BACK>('Watch which? (re-runs on change)', [
      ...verbs.map((v) => ({ value: v.name, label: v.name, hint: verbHint(session, v.name) })),
      { value: BACK, label: '← back' },
    ])
    if (!choice || choice === BACK) return BACK
    const verb = getDevLoopVerb(choice)!
    const pkg = await choosePackage(session, candidatePackages(session, verb), choice)
    if (pkg === BACK) continue
    return runDevLoopForPackage(session, verb, pkg, { watch: true })
  }
}

async function maintenanceMenu(session: Session): Promise<number | typeof BACK> {
  const choice = await selectFrom<string | typeof BACK>('Maintenance', [
    { value: 'install', label: 'install', hint: `${session.workspace.pm} install` },
    { value: 'outdated', label: 'outdated', hint: 'newer versions' },
    { value: 'clean', label: 'clean', hint: 'remove build outputs' },
    { value: 'rebuild', label: 'rebuild', hint: 'clean + build' },
    { value: BACK, label: '← back' },
  ])
  switch (choice) {
    case 'install':
      return install(session)
    case 'outdated':
      return outdated(session)
    case 'clean':
      return clean(session)
    case 'rebuild':
      return rebuild(session)
    default:
      return BACK
  }
}

async function configMenu(session: Session): Promise<number | typeof BACK> {
  const choice = await selectFrom<string | typeof BACK>('Config', [
    { value: 'info', label: 'info', hint: 'what rig discovered' },
    { value: 'doctor', label: 'doctor', hint: 'check the environment' },
    { value: 'setup', label: 'setup', hint: 'set preferences' },
    { value: 'default', label: 'default', hint: 'set the default package' },
    { value: 'init', label: 'init', hint: 'scaffold .rig.json' },
    { value: 'update', label: 'update', hint: 'update rig itself' },
    { value: BACK, label: '← back' },
  ])
  if (!choice || choice === BACK) return BACK
  switch (choice) {
    case 'info':
      return info(session)
    case 'doctor':
      return doctor(session)
    case 'setup':
      return setup(session)
    case 'default':
      return setDefault(session)
    case 'init':
      return init(session)
    case 'update':
      return update(session)
    default:
      return BACK
  }
}
