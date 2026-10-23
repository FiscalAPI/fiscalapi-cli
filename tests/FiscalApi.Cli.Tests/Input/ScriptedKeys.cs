namespace FiscalApi.Cli.Tests.Input;

internal sealed class ScriptedKeys(string typed)
{
    private readonly Queue<char> _pending = new(typed);

    public int Read { get; private set; }

    public ConsoleKeyInfo? TryRead()
    {
        if (!_pending.TryDequeue(out var character))
        {
            return null;
        }

        Read++;
        return character switch
        {
            '\r' => new ConsoleKeyInfo('\r', ConsoleKey.Enter, shift: false, alt: false, control: false),
            '\b' => new ConsoleKeyInfo('\b', ConsoleKey.Backspace, shift: false, alt: false, control: false),
            '\t' => new ConsoleKeyInfo('\t', ConsoleKey.Tab, shift: false, alt: false, control: false),
            _ => new ConsoleKeyInfo(character, ConsoleKey.NoName, shift: false, alt: false, control: false)
        };
    }
}
