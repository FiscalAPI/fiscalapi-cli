using System.Text.Json;

namespace FiscalApi.Cli.Http;

/// <summary>El <c>data</c> de una respuesta exitosa, leído como <see cref="JsonElement"/> para conservar su texto exacto.</summary>
public static class ApiData
{
    /// <summary>
    /// Las rutas que lo usan siempre responden <c>data</c> al tener éxito: sin él (o en <c>null</c>) la API rompió su
    /// contrato, y el CLI lo reporta como un defecto en lugar de imprimir algo vacío.
    /// </summary>
    public static JsonElement Required(JsonElement data) =>
        data.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? throw new InvalidOperationException("La API respondió con éxito pero sin data.")
            : data;
}
