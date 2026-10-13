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

// UTF-8 sin BOM: en Windows la consola usa la página de códigos OEM, que rompe los acentos de los mensajes
// de la API y el JSON que se redirige a otra herramienta. En Windows esto cambia la página de códigos de la
// consola misma, no solo la del proceso; por eso se restaura al salir. Fijar UTF-8 solo en los writers no
// sirve: escribirían bytes UTF-8 que la consola interactiva interpretaría con su página OEM.
var originalOutputEncoding = Console.OutputEncoding;
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
try
{
    // Composición manual (contrato común): sin contenedor DI, todo se pasa por constructor.
    using var cancellation = new CancellationTokenSource();

    // El primer Ctrl+C cancela la operación en curso y el CLI sale con 130; el segundo deja que el sistema
    // termine el proceso, por si algo no respeta la cancelación.
    using var interruption = PosixSignalRegistration.Create(PosixSignal.SIGINT, context =>
    {
        context.Cancel = !cancellation.IsCancellationRequested;
        cancellation.Cancel();
    });

    var console = new CliConsole(Console.Out, Console.Error);
    var globalOptions = new GlobalOptions();

    // El almacén del SO en que corre el binario; FISCALAPI_API_KEY y FISCALAPI_TENANT_KEY ganan sobre él (D-25).
    var credentialStore = CredentialStores.ForCurrentOs(CredentialStores.ProductionService);
    var credentials = new CredentialResolver(credentialStore, Environment.GetEnvironmentVariable);

    // Un solo handler (y su pool de conexiones) para todo el proceso; los comandos reciben apiClients por
    // constructor y piden su cliente con el timeout que necesitan.
    using var httpHandler = FiscalApiClientFactory.CreateHandler();
    var apiClients = new FiscalApiClientFactory(httpHandler, credentials, console.Error, RetryPolicy.Default);

    // Con stdin redirigido no hay una persona que lea un prompt ni teclas que ocultar.
    ISecretReader secretReader = Console.IsInputRedirected
        ? new RedirectedSecretReader(Console.In)
        : new ConsoleSecretReader(console.Error, () => Console.KeyAvailable ? Console.ReadKey(intercept: true) : null);

    // La confirmación de borrar solo se pregunta a una persona en la terminal; un script usa --yes.
    var confirmation = new ConsoleConfirmationPrompt(console.Error, Console.In, isInteractive: !Console.IsInputRedirected);

    IReadOnlyList<Command> commands =
    [
        new LoginCommand(globalOptions, apiClients, credentialStore, secretReader, console),
        new LogoutCommand(globalOptions, credentialStore, console),
        new ListenCommand(globalOptions, apiClients, console, TimeProvider.System, ListenTimings.Default),
        new WebhooksCommand(globalOptions, apiClients, confirmation, console),
        new EventsCommand(globalOptions, apiClients, console)
    ];

    // El aviso de versión nueva comparte el handler (y el proxy del sistema), pero no las credenciales de la API.
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
