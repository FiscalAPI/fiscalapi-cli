namespace FiscalApi.Cli.Tests.Input;

/// <summary>
/// Teclas escritas por una persona simulada, para <c>ConsoleSecretReader</c>. <c>\r</c> es Enter y <c>\b</c> es
/// Backspace. Cuando se acaban, no hay tecla disponible (devuelve <c>null</c>), como una terminal en espera.
/// </summary>
internal sealed class ScriptedKeys(string typed)
{
    private readonly Queue<char> _pending = new(typed);

    /// <summary>Cuántas teclas se leyeron: 0 demuestra que no se le pidió nada a la persona.</summary>
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
