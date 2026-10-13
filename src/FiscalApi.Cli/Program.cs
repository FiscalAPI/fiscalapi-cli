using System.Runtime.InteropServices;
using System.Text;
using FiscalApi.Cli;

// UTF-8 sin BOM: en Windows la consola usa la página de códigos OEM, que rompe los acentos de los mensajes
// de la API y el JSON que se redirige a otra herramienta.
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

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
var application = new CliApplication(new GlobalOptions(), commands: [], console);
return await application.RunAsync(args, cancellation.Token);
