using System.CommandLine.Parsing;

namespace FiscalApi.Cli.Commands;

public static class CommaSeparatedValues
{
    public static IReadOnlyList<string> Parse(ArgumentResult result) =>
        [.. result.Tokens.SelectMany(token => Split(token.Value))];

    public static string[] Split(string value) =>
        value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
