using Spectre.Console;

namespace Rig;

/// <summary>
/// Prints a self-contained shell-completion script for rig. The script calls
/// rig's own <c>[suggest:&lt;pos&gt;]</c> directive directly — no
/// <c>dotnet-suggest</c> broker, no extra install, and identical behaviour on
/// macOS / Linux / Windows (the directive is System.CommandLine, not platform
/// code). Use it via <c>eval "$(rig completion zsh)"</c> (or the pwsh form).
/// </summary>
internal static class CompletionSetup
{
    private static readonly string[] Shells = ["zsh", "bash", "pwsh"];

    public static int Print(string? shell)
    {
        // No shell → show how to enable completion (this is what users run first).
        if (string.IsNullOrWhiteSpace(shell))
        {
            Console.WriteLine(Instructions);
            return 0;
        }

        if (!Shells.Contains(shell, StringComparer.OrdinalIgnoreCase))
        {
            Ui.Error($"Unknown shell '{shell}'. Supported: {string.Join(", ", Shells)}.");
            return 1;
        }

        // Raw stdout — NOT AnsiConsole, which wraps to terminal width and would
        // corrupt the script when captured via `eval "$(rig completion zsh)"`.
        Console.WriteLine(shell.ToLowerInvariant() switch
        {
            "zsh" => Zsh,
            "bash" => Bash,
            _ => Pwsh,
        });
        return 0;
    }

    private const string Instructions = """
        Enable rig tab-completion by adding the line for your shell to its startup file,
        then restarting the shell:

          zsh    ~/.zshrc     eval "$(rig completion zsh)"
          bash   ~/.bashrc    eval "$(rig completion bash)"
          pwsh   $PROFILE     Invoke-Expression (& rig completion pwsh | Out-String)

        That one line generates and loads the completer (it calls rig's built-in
        [suggest] directive — no dotnet-suggest needed). Run `rig completion <shell>`
        to see the script itself.
        """;

    private const string Zsh = """
        # rig zsh completion. Add this ONE line to ~/.zshrc (don't paste this script):
        #     eval "$(rig completion zsh)"
        # It calls rig's built-in [suggest] directive — no dotnet-suggest.
        # Initialize zsh's completion system if it isn't already (no-op on oh-my-zsh etc.).
        (( $+functions[compdef] )) || { autoload -Uz compinit && compinit -u 2>/dev/null }
        _rig() {
          local cl="${words[*]}"
          [[ $CURRENT -gt ${#words[@]} ]] && cl+=" "   # completing a fresh trailing word
          local -a suggestions
          suggestions=(${(f)"$(rig "[suggest:${#cl}]" "$cl" 2>/dev/null | grep -vE '^(-\?|-h|/\?|/h)$')"})
          compadd -a suggestions
        }
        compdef _rig rig
        """;

    private const string Bash = """
        # rig bash completion. Add this line to ~/.bashrc (don't paste this script):
        #     eval "$(rig completion bash)"
        # It calls rig's built-in [suggest] directive — no dotnet-suggest.
        _rig() {
          local IFS=$'\n'
          COMPREPLY=( $(compgen -W "$(rig "[suggest:${COMP_POINT}]" "${COMP_LINE}" 2>/dev/null | grep -vE '^(-\?|-h|/\?|/h)$')" -- "${COMP_WORDS[COMP_CWORD]}") )
        }
        complete -F _rig rig
        """;

    private const string Pwsh = """
        # rig PowerShell completion. Add this line to $PROFILE (don't paste this script):
        #     Invoke-Expression (& rig completion pwsh | Out-String)
        # It calls rig's built-in [suggest] directive — no dotnet-suggest.
        Register-ArgumentCompleter -Native -CommandName rig -ScriptBlock {
          param($wordToComplete, $commandAst, $cursorPosition)
          rig "[suggest:$cursorPosition]" "$commandAst" 2>$null |
            Where-Object { $_ -notmatch '^(-\?|-h|/\?|/h)$' } |
            ForEach-Object {
              [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)
            }
        }
        """;
}
