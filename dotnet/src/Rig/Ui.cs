using Spectre.Console;

namespace Rig;

/// <summary>Centralized Spectre output so verbs render consistently.</summary>
internal static class Ui
{
    /// <summary>When set (via <c>--quiet</c> / <c>quiet</c> config), the
    /// <c>→ command</c> echo is suppressed; results/errors still print.</summary>
    public static bool Quiet { get; set; }

    public static void Command(string file, IEnumerable<string> args)
    {
        if (Quiet) return;
        AnsiConsole.MarkupLine(
            $"[cyan]→ {Markup.Escape(file)} {Markup.Escape(string.Join(' ', args.Select(Exec.QuoteIfNeeded)))}[/]");
    }

    public static void Shell(string command)
    {
        if (Quiet) return;
        AnsiConsole.MarkupLine($"[cyan]→ {Markup.Escape(command)}[/]");
    }

    public static void Info(string message) => AnsiConsole.MarkupLine($"[grey]{Markup.Escape(message)}[/]");
    public static void Warn(string message) => AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(message)}[/]");
    public static void Error(string message) => AnsiConsole.MarkupLine($"[red]{Markup.Escape(message)}[/]");
    public static void Success(string message) => AnsiConsole.MarkupLine($"[green]{Markup.Escape(message)}[/]");
}
