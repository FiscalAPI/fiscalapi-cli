namespace FiscalApi.Cli;

/// <summary>Valores de las opciones globales ya leídos de una invocación.</summary>
public sealed record GlobalSettings(ApiEnvironment Environment, bool Json, bool Verbose);
