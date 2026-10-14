namespace FiscalApi.Cli.Http;

/// <summary>Arma la query string de una ruta: escapa cada valor y omite los que no se dieron.</summary>
public static class QueryString
{
    /// <returns><c>"?a=1&amp;b=2"</c>, o vacío si ningún parámetro tiene valor.</returns>
    public static string From(IEnumerable<KeyValuePair<string, string?>> parameters)
    {
        var pairs = parameters
            .Where(parameter => parameter.Value is not null)
            .Select(parameter => $"{parameter.Key}={Uri.EscapeDataString(parameter.Value!)}")
            .ToList();
        return pairs.Count == 0 ? "" : "?" + string.Join('&', pairs);
    }
}
