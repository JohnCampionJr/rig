using Spectre.Console;

namespace Rig;

/// <summary>Centralized Spectre output so verbs render consistently.</summary>
internal static class Ui
{
    public static void Command(string file, IEnumerable<string> args) =>
        AnsiConsole.MarkupLine(
            $"[cyan]→ {Markup.Escape(file)} {Markup.Escape(string.Join(' ', args.Select(Exec.QuoteIfNeeded)))}[/]");

    public static void Shell(string command) =>
        AnsiConsole.MarkupLine($"[cyan]→ {Markup.Escape(command)}[/]");

    public static void Info(string message) => AnsiConsole.MarkupLine($"[grey]{Markup.Escape(message)}[/]");
    public static void Warn(string message) => AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(message)}[/]");
    public static void Error(string message) => AnsiConsole.MarkupLine($"[red]{Markup.Escape(message)}[/]");
    public static void Success(string message) => AnsiConsole.MarkupLine($"[green]{Markup.Escape(message)}[/]");
}
