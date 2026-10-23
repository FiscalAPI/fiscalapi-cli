using System.CommandLine;
using FiscalApi.Cli.Http;

namespace FiscalApi.Cli.Commands;

public sealed class PageOptions
{
    private readonly Option<int?> _page = new("--page")
    {
        Description = "Page number, from 1. Defaults to 1.",
        HelpName = "number"
    };

    private readonly Option<int?> _pageSize = new("--page-size")
    {
        Description = "Items per page, from 1 to 50. Defaults to 10.",
        HelpName = "size"
    };

    public void AddTo(Command command)
    {
        command.Options.Add(_page);
        command.Options.Add(_pageSize);
    }

    public PageRequest Read(ParseResult parseResult) => new(parseResult.GetValue(_page), parseResult.GetValue(_pageSize));
}
