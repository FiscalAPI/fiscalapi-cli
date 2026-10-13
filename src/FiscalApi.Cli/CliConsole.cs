namespace FiscalApi.Cli;

/// <summary>
/// Salida del CLI: los datos van a <see cref="Out"/> y los diagnósticos y errores a <see cref="Error"/>,
/// para que <c>--json</c> deje solo JSON en stdout.
/// </summary>
public sealed record CliConsole(TextWriter Out, TextWriter Error);
