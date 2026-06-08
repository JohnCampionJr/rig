import { ui } from './ui.js'
import { allScriptNames } from './discovery.js'
import { runDevLoopForPackage, runScriptForPackage } from './dispatch.js'
import { DEV_LOOP_VERBS, applicableDevLoopVerbs, canRunVerb, candidatePackages, describeDevLoop, getDevLoopVerb } from './resolve.js'
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
  | { kind: 'focus'; focus: PackageInfo | null }
  | { kind: 'focusPick' }
  | { kind: 'maintenance' }
  | { kind: 'config' }
  | { kind: 'quit' }

// --- focus scoping ---------------------------------------------------------
// `focus` is the package the menu acts on: a member package (no picker — verbs
// run on it directly) or null (whole repo — the union, with a picker per verb).

function focusedVerbs(session: Session, focus: PackageInfo | null) {
  return focus ? DEV_LOOP_VERBS.filter((v) => canRunVerb(session, v, focus)) : applicableDevLoopVerbs(session)
}
function focusedScripts(session: Session, focus: PackageInfo | null): string[] {
  return focus ? packageExtraScripts(focus) : extraScripts(session)
}
function focusedWatchables(session: Session, focus: PackageInfo | null) {
  return focus
    ? WATCHABLE.map((n) => getDevLoopVerb(n)!).filter((v) => canRunVerb(session, v, focus))
    : watchableVerbs(session)
}
function canCoverHere(session: Session, focus: PackageInfo | null): boolean {
  return focus ? canCover(session, focus) : session.workspace.packages.some((p) => canCover(session, p))
}

/** The top-level menu options for a given focus. Exported for unit testing. */
export function buildOptions(session: Session, focus: PackageInfo | null) {
  const options: Array<{ value: Choice; label: string; hint?: string }> = []
  for (const v of focusedVerbs(session, focus)) {
    const hint = focus ? (describeDevLoop(session, v, focus) ?? undefined) : verbHint(session, v.name)
    options.push({ value: { kind: 'verb', name: v.name }, label: v.name, hint })
  }
  if (canCoverHere(session, focus)) {
    options.push({ value: { kind: 'coverage' }, label: 'coverage', hint: 'tests + coverage' })
  }
  options.push({ value: { kind: 'kill' }, label: 'kill', hint: 'stop dev servers' })
  if (focusedWatchables(session, focus).length) {
    options.push({ value: { kind: 'watch' }, label: 'watch ▸', hint: 'dev/build/test, re-run on change' })
  }
  const scripts = focusedScripts(session, focus)
  if (scripts.length) {
    options.push({ value: { kind: 'scripts' }, label: 'scripts ▸', hint: `${scripts.length} scripts` })
  }
  // Monorepo focus switch: from a package → up to the whole repo; from the whole
  // repo → down into a chosen package (the picker pre-selects where cwd is).
  if (focus) {
    options.push({ value: { kind: 'focus', focus: null }, label: '⌂ whole repo ▸', hint: 'all packages' })
  } else if (session.workspace.isMonorepo) {
    options.push({ value: { kind: 'focusPick' }, label: 'focus a package ▸', hint: 'scope to one package' })
  }
  options.push({ value: { kind: 'maintenance' }, label: 'maintenance ▸', hint: 'install · outdated · clean · rebuild' })
  options.push({ value: { kind: 'config' }, label: 'config ▸' })
  options.push({ value: { kind: 'quit' }, label: 'quit' })
  return options
}

/**
 * The bare-`rig` interactive menu. Defaults to focusing the package cwd is in
 * (monorepo cwd-awareness) with a `⌂ whole repo` escape; at the root / in a
 * single-package repo it's the whole-repo menu. The *selection* phase loops (so
 * `← back`, Esc, and focus switches stay in the menu); the moment a real
 * verb/script is chosen it runs once and returns a process exit code.
 */
export async function runMenu(session: Session): Promise<number> {
  if (!isInteractive()) {
    ui.info('rig — run a verb (or `rig --help`). Interactive menu needs a TTY.')
    ui.info(`verbs: ${DEV_LOOP_VERBS.map((v) => v.name).join(', ')}, kill, info, default, init`)
    return 0
  }

  prompt.intro('rig')
  let focus: PackageInfo | null = session.currentPackage

  for (;;) {
    const title = focus ? `What do you want to run? · ${focus.name}` : 'What do you want to run?'
    const choice = await selectFrom<Choice>(title, buildOptions(session, focus))
    if (!choice || choice.kind === 'quit') {
      prompt.outro('bye')
      return 0
    }

    switch (choice.kind) {
      case 'focus':
        focus = choice.focus // re-render the menu scoped differently
        continue
      case 'focusPick': {
        const members = session.workspace.packages.filter((p) => !p.isRoot)
        const picked = await pickPackage(members, session.config.defaultProject, 'Focus which package?', {
          back: true,
          current: session.currentPackage,
        })
        if (picked && picked !== BACK) focus = picked // else stay in whole-repo
        continue
      }
      case 'verb': {
        const verb = getDevLoopVerb(choice.name)!
        const pkg = focus ?? (await choosePackage(session, candidatePackages(session, verb), choice.name))
        if (pkg === BACK) continue
        return runDevLoopForPackage(session, verb, pkg)
      }
      case 'coverage': {
        const candidates = session.workspace.packages.filter((p) => canCover(session, p))
        const pkg = focus ?? (await choosePackage(session, candidates, 'coverage'))
        if (pkg === BACK) continue
        return coverage(session, { token: pkg.name })
      }
      case 'kill':
        return kill(session)
      case 'watch': {
        const result = await watchMenu(session, focus)
        if (result === BACK) continue
        return result
      }
      case 'scripts': {
        const result = await scriptsMenu(session, focus)
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

/** List a package's scripts and run the chosen one; BACK if backed out. */
async function chooseScript(session: Session, pkg: PackageInfo): Promise<number | typeof BACK> {
  const names = packageExtraScripts(pkg)
  const choice = await selectFrom<string | typeof BACK>(`Which script? (${pkg.name})`, [
    ...names.map((name) => ({ value: name, label: name, hint: pkg.scripts[name] })),
    { value: BACK, label: '← back' },
  ])
  if (!choice || choice === BACK) return BACK
  return runScriptForPackage(session, choice, pkg)
}

/**
 * Scripts group. Focused on a package → that package's scripts directly.
 * Otherwise pick the package first (single-package repos skip that step);
 * backing out of the script list returns to the package picker.
 */
async function scriptsMenu(session: Session, focus: PackageInfo | null): Promise<number | typeof BACK> {
  if (focus) return chooseScript(session, focus)

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
    const result = await chooseScript(session, pkg)
    if (result === BACK) {
      if (single) return BACK // nowhere to go but the top menu
      continue // back to the package picker
    }
    return result
  }
}

/**
 * Watch group: pick a dev/build/test verb, then run it under `--watch`. Focused
 * on a package → runs on it directly; otherwise picks a package. Mirrors the
 * .NET rig's `watch ▸` submenu (the `w`/`watch` prefix and `-w` flag still work).
 */
async function watchMenu(session: Session, focus: PackageInfo | null): Promise<number | typeof BACK> {
  const verbs = focusedWatchables(session, focus)
  for (;;) {
    const choice = await selectFrom<string | typeof BACK>('Watch which? (re-runs on change)', [
      ...verbs.map((v) => ({
        value: v.name,
        label: v.name,
        hint: focus ? (describeDevLoop(session, v, focus) ?? undefined) : verbHint(session, v.name),
      })),
      { value: BACK, label: '← back' },
    ])
    if (!choice || choice === BACK) return BACK
    const verb = getDevLoopVerb(choice)!
    const pkg = focus ?? (await choosePackage(session, candidatePackages(session, verb), choice))
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
