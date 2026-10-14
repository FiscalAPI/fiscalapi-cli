using System.CommandLine.Parsing;

namespace FiscalApi.Cli.Commands;

/// <summary>
/// Las opciones de lista (<c>--events a,b</c>, <c>--person a --person b,c</c>): cada valor se parte por comas y se recortan
/// los espacios. Lo que contiene la lista lo valida la API, con sus textos.
/// </summary>
public static class CommaSeparatedValues
{
    public static IReadOnlyList<string> Parse(ArgumentResult result) =>
        [.. result.Tokens.SelectMany(token => Split(token.Value))];

    /// <returns>Los valores no vacíos de <paramref name="value"/>, sin espacios alrededor.</returns>
    public static string[] Split(string value) =>
        value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
