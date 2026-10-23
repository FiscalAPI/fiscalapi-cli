using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using FiscalApi.Cli.Http;

namespace FiscalApi.Cli.Commands;

public sealed record ApiCommandContext(GlobalOptions GlobalOptions, FiscalApiClientFactory ApiClients, CliConsole Console)
{
    public GlobalSettings Settings(ParseResult parseResult) => GlobalOptions.Read(parseResult);

    public FiscalApiClient Client(GlobalSettings settings) => ApiClients.Create(settings, FiscalApiClientFactory.DefaultTimeout);

    public void WriteData<T>(GlobalSettings settings, JsonElement data, JsonTypeInfo<T> type, Action<TextWriter, T> writeText)
    {
        if (settings.Json)
        {
            WriteRaw(data);
            return;
        }

        writeText(Console.Out, Read(data, type));
    }

    public void WriteRaw(JsonElement data) => Console.Out.WriteLine(data.GetRawText());

    public static T Read<T>(JsonElement data, JsonTypeInfo<T> type) =>
        ApiData.Required(data).Deserialize(type) ?? throw new InvalidOperationException("The API returned no data.");
}
