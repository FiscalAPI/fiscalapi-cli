using System.Text.Json;

namespace FiscalApi.Cli.Http;

public static class ValidationFailures
{
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
