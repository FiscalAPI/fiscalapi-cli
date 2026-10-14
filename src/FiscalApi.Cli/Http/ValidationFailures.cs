using System.Text.Json;

namespace FiscalApi.Cli.Http;

/// <summary>
/// El 400 de validación de fapi-back (<c>GlobalExceptionMiddleware</c>) responde un <c>message</c> y un <c>details</c>
/// genéricos, y el texto de cada regla que falló va en <c>data</c>: un arreglo de <c>ValidationFailure</c> de
/// FluentValidation (<c>propertyName</c>, <c>errorMessage</c>…). Sin esos textos la persona no sabría qué corregir.
/// </summary>
public static class ValidationFailures
{
    /// <returns>El <c>errorMessage</c> de cada falla, o ninguno si <paramref name="data"/> no es ese arreglo.</returns>
    public static IReadOnlyList<string> MessagesIn(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var messages = new List<string>();
        foreach (var failure in data.EnumerateArray())
        {
            if (failure.ValueKind == JsonValueKind.Object
                && failure.TryGetProperty("errorMessage", out var message)
                && message.ValueKind == JsonValueKind.String
                && message.GetString() is { Length: > 0 } text)
            {
                messages.Add(text);
            }
        }

        return messages;
    }
}
