namespace FiscalApi.Cli.Updates;

/// <summary>Aviso de versión nueva que corre en paralelo al comando (CLI-006).</summary>
public interface IUpdateCheck
{
    /// <summary>
    /// Empieza la consulta, si toca, y devuelve en seguida. La tarea nunca falla y termina a más tardar en
    /// <see cref="UpdateCheck.Timeout"/>; su resultado es el aviso que mostrar o <c>null</c>.
    /// </summary>
    Task<UpdateNotice?> Start(GlobalSettings settings, CancellationToken cancellationToken);
}
