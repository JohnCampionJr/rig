using Spectre.Console;
using Spectre.Console.Rendering;

namespace Rig;

/// <summary>Thrown out of a Spectre prompt when the user presses Esc or
/// Backspace, so the menu can treat it as cancel/back. Parity with the Node
/// menu, where Esc cancels natively and backspace is aliased to cancel
/// (see node/src/prompts.ts).</summary>
internal sealed class MenuCancelled : Exception;

/// <summary>Runs Spectre selection prompts on a console whose input lets
/// Esc/Backspace dismiss the prompt (parity with the Node menu). Shared by the
/// bare-`rig` menu and any verb that pops a picker.</summary>
internal static class CancelKeyPrompt
{
    private static readonly IAnsiConsole Console = new CancelKeyConsole(AnsiConsole.Console);

    /// <summary>Show a selection prompt the user can also dismiss with
    /// Esc/Backspace. Returns true with the choice when picked; false (and a
    /// default <paramref name="choice"/>) when dismissed.</summary>
    public static bool TryShow<T>(SelectionPrompt<T> prompt, out T choice) where T : notnull
    {
        try
        {
            choice = prompt.Show(Console);
            return true;
        }
        catch (MenuCancelled)
        {
            AnsiConsole.Console.Cursor.Show(true); // restore the cursor the prompt hid
            choice = default!;
            return false;
        }
    }
}

/// <summary>Wraps a console's input so Esc/Backspace abort the current prompt.
/// Spectre's <see cref="SelectionPrompt{T}"/> ignores both keys by default
/// (search is off), so hijacking them is safe and mirrors the Node menu's
/// cancel keys.</summary>
internal sealed class CancelKeyInput(IAnsiConsoleInput inner) : IAnsiConsoleInput
{
    public bool IsKeyAvailable() => inner.IsKeyAvailable();

    public ConsoleKeyInfo? ReadKey(bool intercept)
    {
        var key = inner.ReadKey(intercept);
        ThrowIfCancel(key);
        return key;
    }

    public async Task<ConsoleKeyInfo?> ReadKeyAsync(bool intercept, CancellationToken cancellationToken)
    {
        var key = await inner.ReadKeyAsync(intercept, cancellationToken).ConfigureAwait(false);
        ThrowIfCancel(key);
        return key;
    }

    private static void ThrowIfCancel(ConsoleKeyInfo? key)
    {
        if (key is { Key: ConsoleKey.Escape or ConsoleKey.Backspace })
            throw new MenuCancelled();
    }
}

/// <summary>A console that behaves exactly like the one it wraps, except its
/// input lets Esc/Backspace cancel a prompt (see <see cref="CancelKeyInput"/>).
/// Everything else forwards to the inner console unchanged.</summary>
internal sealed class CancelKeyConsole(IAnsiConsole inner) : IAnsiConsole
{
    private readonly IAnsiConsoleInput input = new CancelKeyInput(inner.Input);

    public Profile Profile => inner.Profile;
    public IAnsiConsoleCursor Cursor => inner.Cursor;
    public IAnsiConsoleInput Input => input;
    public IExclusivityMode ExclusivityMode => inner.ExclusivityMode;
    public RenderPipeline Pipeline => inner.Pipeline;

    public void Clear(bool home) => inner.Clear(home);
    public void Write(IRenderable renderable) => inner.Write(renderable);
    public void WriteAnsi(Action<AnsiWriter> action) => inner.WriteAnsi(action);
}
