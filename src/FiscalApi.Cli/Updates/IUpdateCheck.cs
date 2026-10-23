namespace FiscalApi.Cli.Updates;

public interface IUpdateCheck
{
    Task<UpdateNotice?> Start(GlobalSettings settings, CancellationToken cancellationToken);
}
