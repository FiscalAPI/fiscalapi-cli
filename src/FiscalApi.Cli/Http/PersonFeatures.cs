namespace FiscalApi.Cli.Http;

/// <summary>
/// Lo que el CLI lee de <c>GET /api/v4/me/features</c> (<c>PersonFeaturesDto</c> de fapi-back): solo los permisos
/// concedidos. Un permiso que los features del tenant niegan va en <c>denied</c>, no aquí.
/// </summary>
public sealed class PersonFeatures
{
    public IReadOnlyList<string> Permissions { get; init; } = [];
}
