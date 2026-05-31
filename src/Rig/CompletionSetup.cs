using Spectre.Console;

namespace Rig;

/// <summary>
/// Prints the one-time shell setup for completion. `rig` participates in the
/// standard <c>dotnet-suggest</c> mechanism (it discovers System.CommandLine
/// global tools automatically), so the per-shell snippet wires the shell to
/// <c>dotnet-suggest</c> rather than to `rig` directly.
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

        AnsiConsole.WriteLine("# 1. Install the completion broker once:");
        AnsiConsole.WriteLine("#    dotnet tool install --global dotnet-suggest");
        AnsiConsole.WriteLine("# 2. Add the snippet below to your shell profile, then restart your shell.");
        AnsiConsole.WriteLine();

        AnsiConsole.WriteLine(shell.ToLowerInvariant() switch
        {
            "zsh" => Zsh,
            "bash" => Bash,
            _ => Pwsh,
        });
        return 0;
    }

    private const string Bash = """
        _dotnet_suggest() {
          local completions=$(dotnet-suggest get --executable "${COMP_WORDS[0]}" --position ${COMP_POINT} -- "${COMP_LINE}")
          COMPREPLY=( $(compgen -W "$completions" -- "${COMP_WORDS[COMP_CWORD]}") )
        }
        complete -F _dotnet_suggest rig
        export DOTNET_SUGGEST_SCRIPT_VERSION="1.0.0"
        """;

    private const string Zsh = """
        _dotnet_suggest() {
          local completions=$(dotnet-suggest get --executable "${words[1]}" --position $CURSOR -- "${BUFFER}")
          reply=( ${(ps:\n:)completions} )
        }
        compctl -K _dotnet_suggest rig
        export DOTNET_SUGGEST_SCRIPT_VERSION="1.0.0"
        """;

    private const string Pwsh = """
        Register-ArgumentCompleter -Native -CommandName rig -ScriptBlock {
          param($wordToComplete, $commandAst, $cursorPosition)
          dotnet-suggest get --executable "rig" --position $cursorPosition -- "$commandAst" | ForEach-Object {
            [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)
          }
        }
        $env:DOTNET_SUGGEST_SCRIPT_VERSION = "1.0.0"
        """;
}
