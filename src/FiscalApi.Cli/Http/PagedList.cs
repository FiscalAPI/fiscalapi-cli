namespace FiscalApi.Cli.Http;

public sealed class PagedList<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];

    public int PageNumber { get; init; }

    public int TotalPages { get; init; }

    public int TotalCount { get; init; }

    public bool HasNextPage { get; init; }
}

public sealed record PageRequest(int? Number, int? Size)
{
    public IEnumerable<KeyValuePair<string, string?>> QueryParameters =>
    [
        KeyValuePair.Create("pageNumber", Number?.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        KeyValuePair.Create("pageSize", Size?.ToString(System.Globalization.CultureInfo.InvariantCulture))
    ];
}
