using System.Text.Json;

namespace FiscalApi.Cli.Http;

public static class ApiData
{
    public static JsonElement Required(JsonElement data) =>
        data.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? throw new InvalidOperationException("The API returned a success response without data.")
            : data;
}
