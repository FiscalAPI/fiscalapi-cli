using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using FiscalApi.Cli.Http;

namespace FiscalApi.Cli.Commands;

/// <summary>
/// Lo que comparten los comandos que llaman a la API (<c>webhooks</c> y <c>events</c>): las opciones globales, la fábrica
/// de clientes y la salida, más la única regla de salida de sus datos (<see cref="WriteData{T}"/>).
/// </summary>
public sealed record ApiCommandContext(GlobalOptions GlobalOptions, FiscalApiClientFactory ApiClients, CliConsole Console)
{
    public GlobalSettings Settings(ParseResult parseResult) => GlobalOptions.Read(parseResult);

    public FiscalApiClient Client(GlobalSettings settings) => ApiClients.Create(settings, FiscalApiClientFactory.DefaultTimeout);

    /// <summary>
    /// Con <c>--json</c>, el <c>data</c> de la respuesta exactamente como lo escribió la API (contrato común); sin él, la
    /// vista de texto del recurso leído con <paramref name="type"/>.
    /// </summary>
    public void WriteData<T>(GlobalSettings settings, JsonElement data, JsonTypeInfo<T> type, Action<TextWriter, T> writeText)
    {
        if (settings.Json)
        {
            WriteRaw(data);
            return;
        }

        writeText(Console.Out, Read(data, type));
    }

    /// <summary>Como la otra sobrecarga, cuando el comando ya leyó el recurso porque necesita algo más de él.</summary>
    public void WriteData<T>(GlobalSettings settings, JsonElement data, T value, Action<TextWriter, T> writeText)
    {
        if (settings.Json)
        {
            WriteRaw(data);
            return;
        }

        writeText(Console.Out, value);
    }

    /// <summary>El JSON del <c>data</c> sin re-serializarlo: los mismos caracteres que mandó la API.</summary>
    public void WriteRaw(JsonElement data) => Console.Out.WriteLine(data.GetRawText());

    public static T Read<T>(JsonElement data, JsonTypeInfo<T> type) =>
        data.Deserialize(type) ?? throw new InvalidOperationException("La API respondió con éxito pero sin data.");
}
