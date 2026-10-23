namespace FiscalApi.Cli.Http;

public static class QueryString
{
    public static string From(IEnumerable<KeyValuePair<string, string?>> parameters)
    {
        var pairs = parameters
            .Select(parameter => parameter.Value is { } value ? $"{parameter.Key}={Uri.EscapeDataString(value)}" : null)
            .OfType<string>()
            .ToList();
        return pairs.Count == 0 ? "" : "?" + string.Join('&', pairs);
    }
}
