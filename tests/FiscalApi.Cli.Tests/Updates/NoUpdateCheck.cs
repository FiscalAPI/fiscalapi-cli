using FiscalApi.Cli.Updates;

namespace FiscalApi.Cli.Tests.Updates;

public sealed class NoUpdateCheck : IUpdateCheck
{
    public static readonly NoUpdateCheck Instance = new();

    private NoUpdateCheck()
    {
    }

    public Task<UpdateNotice?> Start(GlobalSettings settings, CancellationToken cancellationToken) =>
        Task.FromResult<UpdateNotice?>(null);
}
