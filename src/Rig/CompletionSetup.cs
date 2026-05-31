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
        if (string.IsNullOrWhiteSpace(shell) || !Shells.Contains(shell, StringComparer.OrdinalIgnoreCase))
        {
            Ui.Error($"Usage: rig completion <{string.Join('|', Shells)}>");
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

    // zsh: add to ~/.zshrc →  eval "$(rig completion zsh)"
    private const string Zsh = """
        # rig shell completion — calls rig's built-in [suggest] directive (no dotnet-suggest).
        _rig() {
          local cl="${words[*]}"
          [[ $CURRENT -gt ${#words[@]} ]] && cl+=" "   # completing a fresh trailing word
          local -a suggestions
          suggestions=(${(f)"$(rig "[suggest:${#cl}]" "$cl" 2>/dev/null)"})
          compadd -a suggestions
        }
        compdef _rig rig
        """;

    // bash: add to ~/.bashrc →  eval "$(rig completion bash)"
    private const string Bash = """
        # rig shell completion — calls rig's built-in [suggest] directive (no dotnet-suggest).
        _rig() {
          local IFS=$'\n'
          COMPREPLY=( $(compgen -W "$(rig "[suggest:${COMP_POINT}]" "${COMP_LINE}" 2>/dev/null)" -- "${COMP_WORDS[COMP_CWORD]}") )
        }
        complete -F _rig rig
        """;

    // pwsh: add to $PROFILE →  Invoke-Expression (& rig completion pwsh | Out-String)
    private const string Pwsh = """
        # rig shell completion — calls rig's built-in [suggest] directive (no dotnet-suggest).
        Register-ArgumentCompleter -Native -CommandName rig -ScriptBlock {
          param($wordToComplete, $commandAst, $cursorPosition)
          rig "[suggest:$cursorPosition]" "$commandAst" 2>$null | ForEach-Object {
            [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)
          }
        }
        """;
}
