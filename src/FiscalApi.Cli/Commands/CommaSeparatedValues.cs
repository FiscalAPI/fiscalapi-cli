using System.CommandLine.Parsing;

namespace FiscalApi.Cli.Commands;

/// <summary>
/// Las opciones de lista (<c>--events a,b</c>, <c>--person a --person b,c</c>): cada valor se parte por comas y se recortan
/// los espacios. Una lista vacía es válida (p. ej. <c>--person ""</c>); lo que contiene lo valida la API, con sus textos.
/// </summary>
public static class CommaSeparatedValues
{
    public static IReadOnlyList<string> Parse(ArgumentResult result) =>
        [.. result.Tokens.SelectMany(token =>
            token.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))];
}
