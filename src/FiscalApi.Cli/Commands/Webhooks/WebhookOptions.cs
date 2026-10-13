using System.CommandLine;

namespace FiscalApi.Cli.Commands.Webhooks;

/// <summary>
/// Lo que se escribe de un webhook en el alta y en el cambio (<c>IWebhookInput</c> de fapi-back): URL,
/// descripción, tipos de evento y personas. La API valida cada valor con sus textos (URL https, tipos que existen, '*'
/// solo, hasta 100 personas).
/// </summary>
public sealed class WebhookOptions
{
    /// <param name="required">En el alta la URL y los tipos son obligatorios; en el cambio, cada opción es opcional.</param>
    public WebhookOptions(bool required)
    {
        Url.Required = required;
        Events.Required = required;
    }

    public Option<string> Url { get; } = new("--url")
    {
        Description = "URL https que recibe las entregas.",
        HelpName = "url"
    };

    public Option<string> Description { get; } = new("--description")
    {
        Description = "Descripción libre, hasta 256 caracteres.",
        HelpName = "texto"
    };

    public Option<IReadOnlyList<string>> Events { get; } = new("--events")
    {
        Description = "Tipos de evento separados por coma (p. ej. invoice.canceled,invoice.cancellation_rejected), o * para todos.",
        HelpName = "tipos",
        Arity = ArgumentArity.ExactlyOne,
        CustomParser = CommaSeparatedValues.Parse
    };

    public Option<IReadOnlyList<string>> Persons { get; } = new("--person")
    {
        Description = "Id de una persona cuyos eventos recibe; se repite o va separado por coma. Sin personas, recibe los de todas.",
        HelpName = "id",
        Arity = ArgumentArity.OneOrMore,
        CustomParser = CommaSeparatedValues.Parse
    };

    public void AddTo(Command command)
    {
        command.Options.Add(Url);
        command.Options.Add(Description);
        command.Options.Add(Events);
        command.Options.Add(Persons);
    }

    /// <summary>El valor de <paramref name="option"/> solo si la persona la escribió; si no, <c>null</c> ("no lo cambies").</summary>
    public static T? GivenValue<T>(ParseResult parseResult, Option<T> option) =>
        parseResult.GetResult(option) is { Implicit: false } ? parseResult.GetValue(option) : default;
}
