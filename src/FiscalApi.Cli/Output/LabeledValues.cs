namespace FiscalApi.Cli.Output;

/// <summary>
/// El detalle de un recurso: una línea por dato, con las etiquetas alineadas. Los datos sin valor no se muestran: lo que
/// no aplica (p. ej. el motivo de un webhook habilitado) solo estorbaría.
/// </summary>
public static class LabeledValues
{
    private const string Separator = "  ";

    public static void Write(TextWriter output, IEnumerable<(string Label, string? Value)> values)
    {
        var shown = values.Where(value => !string.IsNullOrEmpty(value.Value)).ToList();
        var width = shown.Count == 0 ? 0 : shown.Max(value => value.Label.Length);
        foreach (var (label, value) in shown)
        {
            output.WriteLine($"{label.PadRight(width)}{Separator}{TextTable.Cell(value)}");
        }
    }
}
