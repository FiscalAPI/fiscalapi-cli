using System.CommandLine;
using System.CommandLine.Parsing;

namespace FiscalApi.Cli.Commands.Webhooks;

public sealed class WebhookOptions
{
    public WebhookOptions(bool required)
    {
        Url.Required = required;
        Events.Required = required;
    }

    public Option<string> Url { get; } = new("--url")
    {
        Description = "HTTPS URL that receives the deliveries.",
        HelpName = "url"
    };

    public Option<string> Description { get; } = new("--description")
    {
        Description = "Free-text description, up to 256 characters.",
        HelpName = "text"
    };

    public Option<IReadOnlyList<string>> Events { get; } = new("--events")
    {
        Description = "Comma-separated event types, e.g. invoice.canceled,invoice.cancellation_rejected, or * for all.",
        HelpName = "types",
        Arity = ArgumentArity.ExactlyOne,
        CustomParser = CommaSeparatedValues.Parse
    };

    public Option<IReadOnlyList<string>> Persons { get; } = new("--person")
    {
        Description = "ID of a person whose events the webhook receives. Repeat it or separate IDs with commas.",
        HelpName = "id",
        Arity = ArgumentArity.OneOrMore,
        CustomParser = ParsePersons
    };

    public Option<bool> AllPersons { get; } = new("--all-persons")
    {
        Description = "Receive events for all persons. Removes the --person filter."
    };

    public void AddTo(Command command)
    {
        command.Options.Add(Url);
        command.Options.Add(Description);
        command.Options.Add(Events);
        command.Options.Add(Persons);
        command.Options.Add(AllPersons);
        command.Validators.Add(RejectAllPersonsWithPersons);
    }

    public static T? GivenValue<T>(ParseResult parseResult, Option<T> option) =>
        parseResult.GetResult(option) is { Implicit: false } ? parseResult.GetValue(option) : default;

    private static IReadOnlyList<string> ParsePersons(ArgumentResult result)
    {
        if (result.Tokens.Any(token => CommaSeparatedValues.Split(token.Value).Length == 0))
        {
            result.AddError("--person needs at least one person ID. Use --all-persons to receive events for all persons.");
        }

        return CommaSeparatedValues.Parse(result);
    }

    private void RejectAllPersonsWithPersons(CommandResult result)
    {
        if (result.GetResult(AllPersons) is { Implicit: false } && result.GetResult(Persons) is { Implicit: false })
        {
            result.AddError("--all-persons cannot be used with --person.");
        }
    }
}
