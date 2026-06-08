import * as clack from '@clack/prompts'
import type { PackageInfo } from './types.js'

/** Sentinel returned by a back-aware prompt when the user chooses "← back". */
export const BACK = Symbol('back')

/** Whether we can drive an interactive prompt (both ends are a TTY). */
export function isInteractive(): boolean {
  return Boolean(process.stdout.isTTY && process.stdin.isTTY)
}

export interface Choice<T> {
  value: T
  label: string
  hint?: string
}

/**
 * A single-select prompt. Returns null when the user cancels (Ctrl-C / Esc /
 * Backspace). Backspace is bound to cancel only for the duration of the prompt,
 * so it never interferes with text inputs elsewhere.
 */
export async function selectFrom<T>(
  message: string,
  choices: Choice<T>[],
  initialValue?: T,
): Promise<T | null> {
  clack.settings.aliases.set('backspace', 'cancel')
  try {
    const result = await clack.select<T>({
      message,
      options: choices as never,
      initialValue: initialValue as never,
      maxItems: 14,
    })
    if (clack.isCancel(result)) return null
    return result as T
  } finally {
    clack.settings.aliases.delete('backspace')
  }
}

/**
 * Pick a package, marking the default and showing its relative path. With
 * `back: true`, appends a "← back" row; returns BACK when chosen (or cancelled).
 */
export async function pickPackage(
  packages: PackageInfo[],
  defaultName: string | undefined,
  message = 'Which package?',
  opts: { back?: boolean } = {},
): Promise<PackageInfo | typeof BACK | null> {
  const def = defaultName
    ? packages.find((p) => p.name === defaultName || p.name.endsWith(`/${defaultName}`))
    : undefined
  const choices: Choice<PackageInfo | typeof BACK>[] = packages.map((p) => ({
    value: p,
    label: p === def ? `${p.name} (default)` : p.name,
    hint: p.isRoot ? 'root' : p.relDir,
  }))
  if (opts.back) choices.push({ value: BACK, label: '← back' })
  const picked = await selectFrom(message, choices, def ?? packages[0])
  return picked
}

export const prompt = clack
