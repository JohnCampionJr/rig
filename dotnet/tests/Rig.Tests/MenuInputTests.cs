using Spectre.Console;

namespace Rig.Tests;

/// <summary>The menu's Esc/Backspace cancel keys (parity with the Node menu).
/// Drives <see cref="CancelKeyInput"/> with scripted keys — no TTY needed.</summary>
[TestClass]
public sealed class MenuInputTests
{
    // A scripted input that hands back a fixed queue of keys, newest call first.
    private sealed class FakeInput(params ConsoleKeyInfo[] keys) : IAnsiConsoleInput
    {
        private readonly Queue<ConsoleKeyInfo> queue = new(keys);
        public bool IsKeyAvailable() => queue.Count > 0;
        public ConsoleKeyInfo? ReadKey(bool intercept) => queue.Dequeue();
        public Task<ConsoleKeyInfo?> ReadKeyAsync(bool intercept, CancellationToken ct) =>
            Task.FromResult<ConsoleKeyInfo?>(queue.Dequeue());
    }

    private static ConsoleKeyInfo Key(ConsoleKey key) => new('\0', key, false, false, false);

    [TestMethod]
    public void Escape_cancels_the_prompt()
    {
        var input = new CancelKeyInput(new FakeInput(Key(ConsoleKey.Escape)));
        var act = () => input.ReadKey(true);
        act.Should().Throw<MenuCancelled>();
    }

    [TestMethod]
    public void Backspace_cancels_the_prompt()
    {
        var input = new CancelKeyInput(new FakeInput(Key(ConsoleKey.Backspace)));
        var act = () => input.ReadKey(true);
        act.Should().Throw<MenuCancelled>();
    }

    [TestMethod]
    public void Other_keys_pass_through_untouched()
    {
        var input = new CancelKeyInput(new FakeInput(Key(ConsoleKey.Enter), Key(ConsoleKey.DownArrow)));
        input.ReadKey(true)!.Value.Key.Should().Be(ConsoleKey.Enter);
        input.ReadKey(true)!.Value.Key.Should().Be(ConsoleKey.DownArrow);
    }

    [TestMethod]
    public async Task Cancel_keys_also_apply_to_the_async_read_path()
    {
        var input = new CancelKeyInput(new FakeInput(Key(ConsoleKey.Escape)));
        var act = async () => await input.ReadKeyAsync(true, CancellationToken.None);
        await act.Should().ThrowAsync<MenuCancelled>();
    }
}
