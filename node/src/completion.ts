/**
 * Prints a self-contained shell-completion script. The script calls
 * `rig [suggest:N] "<line>"` — the shared completion protocol both rigs speak —
 * so it works no matter which `rig` wins on PATH and forwards across ecosystems
 * automatically (a .NET dir answers from the .NET tool, a Node dir from this
 * one). Identical in shape to the .NET rig's script by design.
 *
 * Enable with `eval "$(rig completion zsh)"` (or the pwsh form).
 */

const SHELLS = ['zsh', 'bash', 'pwsh'] as const

const INSTRUCTIONS = `Enable rig tab-completion by adding the line for your shell to its startup file,
then restarting the shell:

  zsh    ~/.zshrc     eval "$(rig completion zsh)"
  bash   ~/.bashrc    eval "$(rig completion bash)"
  pwsh   $PROFILE     Invoke-Expression (& rig completion pwsh | Out-String)

That one line generates and loads the completer (it calls rig's shared
[suggest] protocol — the same one the .NET rig uses, so a single script
completes both). Run \`rig completion <shell>\` to see the script itself.`

const ZSH = `# rig zsh completion. Add this ONE line to ~/.zshrc (don't paste this script):
#     eval "$(rig completion zsh)"
# It calls rig's shared [suggest] protocol — one script completes both rigs.
# Initialize zsh's completion system if it isn't already (no-op on oh-my-zsh etc.).
(( $+functions[compdef] )) || { autoload -Uz compinit && compinit -u 2>/dev/null }
_rig() {
  local cl="\${words[*]}"
  [[ $CURRENT -gt \${#words[@]} ]] && cl+=" "   # completing a fresh trailing word
  local -a suggestions
  suggestions=(\${(f)"$(rig "[suggest:\${#cl}]" "$cl" 2>/dev/null | grep -vE '^(-\\?|-h|/\\?|/h)$')"})
  compadd -a suggestions
}
compdef _rig rig rignode rigdotnet

# \`rig cd\` integration. A subprocess can't change the parent shell's directory,
# so wrap rig: \`rig cd [query]\` prints a package dir (its menu goes to stderr)
# and we cd to it. Non-cd calls pass straight through to the binary.
rig() {
  if [ "$1" = cd ]; then
    local __rig_dir
    __rig_dir="$(command rig "$@")" && [ -n "$__rig_dir" ] && builtin cd -- "$__rig_dir"
  else
    command rig "$@"
  fi
}`

const BASH = `# rig bash completion. Add this line to ~/.bashrc (don't paste this script):
#     eval "$(rig completion bash)"
# It calls rig's shared [suggest] protocol — one script completes both rigs.
_rig() {
  local IFS=$'\\n'
  COMPREPLY=( $(compgen -W "$(rig "[suggest:\${COMP_POINT}]" "\${COMP_LINE}" 2>/dev/null | grep -vE '^(-\\?|-h|/\\?|/h)$')" -- "\${COMP_WORDS[COMP_CWORD]}") )
}
complete -F _rig rig rignode rigdotnet

# \`rig cd\` integration — wrap rig so \`rig cd [query]\` can change the directory.
rig() {
  if [ "$1" = cd ]; then
    local __rig_dir
    __rig_dir="$(command rig "$@")" && [ -n "$__rig_dir" ] && builtin cd -- "$__rig_dir"
  else
    command rig "$@"
  fi
}`

const PWSH = `# rig PowerShell completion. Add this line to $PROFILE (don't paste this script):
#     Invoke-Expression (& rig completion pwsh | Out-String)
# It calls rig's shared [suggest] protocol — one script completes both rigs.
Register-ArgumentCompleter -Native -CommandName rig,rignode,rigdotnet -ScriptBlock {
  param($wordToComplete, $commandAst, $cursorPosition)
  rig "[suggest:$cursorPosition]" "$commandAst" 2>$null |
    Where-Object { $_ -notmatch '^(-\\?|-h|/\\?|/h)$' } |
    ForEach-Object {
      [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)
    }
}

# \`rig cd\` integration — wrap rig so \`rig cd [query]\` can change the directory.
# (The native completer above still fires for this function — verified on
# PowerShell 7 — so \`rig <TAB>\` keeps completing too.)
function rig {
  $exe = Get-Command -CommandType Application rig -ErrorAction SilentlyContinue | Select-Object -First 1
  if (-not $exe) { return }
  if ($args.Count -gt 0 -and $args[0] -eq 'cd') {
    $dir = & $exe.Source @args
    if ($LASTEXITCODE -eq 0 -and $dir) { Set-Location -LiteralPath $dir }
  } else {
    & $exe.Source @args
  }
}`

/** Print the completion script for `shell`, or usage instructions if none given. */
export function printCompletion(shell: string | undefined): number {
  if (!shell) {
    process.stdout.write(INSTRUCTIONS + '\n')
    return 0
  }
  const s = shell.toLowerCase()
  if (!SHELLS.includes(s as (typeof SHELLS)[number])) {
    process.stderr.write(`Unknown shell '${shell}'. Supported: ${SHELLS.join(', ')}.\n`)
    return 1
  }
  process.stdout.write((s === 'zsh' ? ZSH : s === 'bash' ? BASH : PWSH) + '\n')
  return 0
}
