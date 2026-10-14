using System.CommandLine;
using System.CommandLine.Parsing;

namespace FiscalApi.Cli;

/// <summary>
/// Opciones globales del contrato común. Son recursivas: valen en el comando raíz y en todos los subcomandos.
/// </summary>
public sealed class GlobalOptions
{
    private readonly Option<bool> _live = new("--live")
    {
        Description = "Usa el ambiente de producción (https://live.fiscalapi.com) en lugar de test.",
        Recursive = true
    };

    private readonly Option<Uri?> _apiBase = new("--api-base")
    {
        Description = "URL base de la API (p. ej. el back local). Gana sobre --live.",
        HelpName = "url",
        Recursive = true,
        CustomParser = ParseApiBase
    };

    private readonly Option<bool> _json = new("--json")
    {
        Description = "Escribe solo JSON en stdout.",
        Recursive = true
    };

    private readonly Option<bool> _verbose = new("--verbose")
    {
        Description = "Muestra el detalle HTTP en stderr, sin headers de credenciales.",
        Recursive = true
    };

    public void AddTo(Command command)
    {
        command.Options.Add(_live);
        command.Options.Add(_apiBase);
        command.Options.Add(_json);
        command.Options.Add(_verbose);
    }

    public GlobalSettings Read(ParseResult parseResult) => new(
        ApiEnvironment.Resolve(parseResult.GetValue(_apiBase), parseResult.GetValue(_live)),
        parseResult.GetValue(_json),
        parseResult.GetValue(_verbose));

    // Solo URLs absolutas http/https: una URL relativa o de otro esquema terminaría en un error de red confuso.
    private static Uri? ParseApiBase(ArgumentResult result)
    {
        var value = result.Tokens.Single().Value;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            result.AddError($"--api-base debe ser una URL absoluta http o https; se recibió '{value}'.");
            return null;
        }

        // La API key y la tenant key viajan en headers: sin TLS solo se permite el back local (localhost,
        // 127.0.0.0/8 o ::1), que no sale del equipo.
        if (uri.Scheme == Uri.UriSchemeHttp && !uri.IsLoopback)
        {
            result.AddError($"--api-base con http solo se permite para localhost, 127.0.0.1 o ::1; usa https para '{value}'.");
            return null;
        }

        return uri;
    }
}
