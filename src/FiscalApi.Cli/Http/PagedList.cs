namespace FiscalApi.Cli.Http;

/// <summary>
/// Una página de un listado de fapi-back (<c>WebApi.Libs.Application.Common.Models.PagedList&lt;T&gt;</c>): <c>items</c>,
/// <c>pageNumber</c>, <c>totalPages</c>, <c>totalCount</c>, <c>hasPreviousPage</c> y <c>hasNextPage</c>. Solo para la
/// salida de texto: con <c>--json</c> la página se imprime tal como llegó.
/// </summary>
public sealed class PagedList<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];

    public int PageNumber { get; init; }

    public int TotalPages { get; init; }

    public int TotalCount { get; init; }

    public bool HasNextPage { get; init; }
}

/// <summary>La página que pide la persona; lo que no da lo decide la API (página 1 de 10).</summary>
public sealed record PageRequest(int? Number, int? Size)
{
    /// <summary>Los parámetros de fapi-back (<c>pageNumber</c>, <c>pageSize</c>); los rangos los valida la API.</summary>
    public IEnumerable<KeyValuePair<string, string?>> QueryParameters =>
    [
        KeyValuePair.Create("pageNumber", Number?.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        KeyValuePair.Create("pageSize", Size?.ToString(System.Globalization.CultureInfo.InvariantCulture))
    ];
}
