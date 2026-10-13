using System.CommandLine;
using FiscalApi.Cli.Http;

namespace FiscalApi.Cli.Commands;

/// <summary><c>--page</c> y <c>--page-size</c> de los listados. Los rangos (1–50 por página) los valida la API.</summary>
public sealed class PageOptions
{
    private readonly Option<int?> _page = new("--page")
    {
        Description = "Número de página, desde 1 (por omisión 1).",
        HelpName = "número"
    };

    private readonly Option<int?> _pageSize = new("--page-size")
    {
        Description = "Elementos por página, de 1 a 50 (por omisión 10).",
        HelpName = "tamaño"
    };

    public void AddTo(Command command)
    {
        command.Options.Add(_page);
        command.Options.Add(_pageSize);
    }

    public PageRequest Read(ParseResult parseResult) => new(parseResult.GetValue(_page), parseResult.GetValue(_pageSize));
}
