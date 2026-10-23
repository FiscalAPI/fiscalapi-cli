using System.CommandLine;
using System.Runtime.InteropServices;
using System.Text;
using FiscalApi.Cli;
using FiscalApi.Cli.Commands;
using FiscalApi.Cli.Commands.Events;
using FiscalApi.Cli.Commands.Webhooks;
using FiscalApi.Cli.Credentials;
using FiscalApi.Cli.Http;
using FiscalApi.Cli.Input;
using FiscalApi.Cli.Listen;
using FiscalApi.Cli.Updates;

var originalOutputEncoding = Console.OutputEncoding;
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
try
{
    using var cancellation = new CancellationTokenSource();

    using var interruption = PosixSignalRegistration.Create(PosixSignal.SIGINT, context =>
    {
        context.Cancel = !cancellation.IsCancellationRequested;
        cancellation.Cancel();
    });

    var console = new CliConsole(Console.Out, Console.Error);
    var globalOptions = new GlobalOptions();

    var credentialStore = CredentialStores.ForCurrentOs(CredentialStores.ProductionService);
    var credentials = new CredentialResolver(credentialStore, Environment.GetEnvironmentVariable);

    using var httpHandler = FiscalApiClientFactory.CreateHandler();
    var apiClients = new FiscalApiClientFactory(httpHandler, credentials, console.Error, RetryPolicy.Default);

    ISecretReader secretReader = Console.IsInputRedirected
        ? new RedirectedSecretReader(Console.In)
        : new ConsoleSecretReader(console.Error, () => Console.KeyAvailable ? Console.ReadKey(intercept: true) : null);

    var confirmation = new ConsoleConfirmationPrompt(console.Error, Console.In, isInteractive: !Console.IsInputRedirected);

    IReadOnlyList<Command> commands =
    [
        new LoginCommand(globalOptions, apiClients, credentialStore, secretReader, console),
        new LogoutCommand(globalOptions, credentialStore, console),
        new ListenCommand(globalOptions, apiClients, console, TimeProvider.System, ListenTimings.Default),
        new TriggerCommand(globalOptions, apiClients, console),
        new WebhooksCommand(globalOptions, apiClients, confirmation, console),
        new EventsCommand(globalOptions, apiClients, console)
    ];

    var updateCheck = new UpdateCheck(
        httpHandler,
        UpdateCheckStateFile.ForCurrentUser(Environment.GetEnvironmentVariable),
        Environment.GetEnvironmentVariable,
        Console.IsErrorRedirected,
        TimeProvider.System,
        CliVersion.Current);

    var application = new CliApplication(globalOptions, commands, console, updateCheck);
    return await application.RunAsync(args, cancellation.Token);
}
finally
{
    Console.OutputEncoding = originalOutputEncoding;
}
