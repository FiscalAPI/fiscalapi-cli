namespace FiscalApi.Cli.Output;

public static class TextTable
{
    public const string Missing = "-";

    private const string ColumnSeparator = "  ";

    public static void Write(TextWriter output, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string?>> rows)
    {
        var lines = rows.Select(row => row.Select(Cell).ToArray()).Prepend([.. headers]).ToList();
        var widths = headers.Select((_, column) => lines.Max(line => line[column].Length)).ToArray();
        foreach (var line in lines)
        {
            output.WriteLine(string.Join(ColumnSeparator, line.Select((cell, column) => cell.PadRight(widths[column]))).TrimEnd());
        }
    }

    public static string Cell(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Missing
            : string.Create(value.Length, value, (span, source) =>
            {
                for (var i = 0; i < source.Length; i++)
                {
                    span[i] = char.IsControl(source[i]) ? ' ' : source[i];
                }
            });
}
