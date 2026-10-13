using FiscalApi.Cli.Updates;

namespace FiscalApi.Cli.Tests.Updates;

/// <summary>Para las pruebas de los comandos: ningún aviso de versión nueva y ninguna consulta a GitHub.</summary>
public sealed class NoUpdateCheck : IUpdateCheck
{
    public static readonly NoUpdateCheck Instance = new();

    private NoUpdateCheck()
    {
    }

    public Task<UpdateNotice?> Start(GlobalSettings settings, CancellationToken cancellationToken) =>
        Task.FromResult<UpdateNotice?>(null);
}
