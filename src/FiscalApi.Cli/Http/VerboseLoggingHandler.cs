using System.Diagnostics;
using System.Net.Http.Headers;

namespace FiscalApi.Cli.Http;

/// <summary>
/// <c>--verbose</c>: escribe cada request y su respuesta en stderr. Los headers de credenciales nunca se escriben,
/// ni siquiera enmascarados, porque la salida suele pegarse en tickets y chats de soporte. Los cuerpos tampoco:
/// pueden traer secretos (p. ej. el de un webhook).
/// </summary>
public sealed class VerboseLoggingHandler(TextWriter diagnostics) : DelegatingHandler
{
    private static readonly HashSet<string> CredentialHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "X-API-KEY",
        "X-TENANT-KEY",
        "Authorization",
        "Proxy-Authorization",
        "Cookie",
        "Set-Cookie"
    };

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var line = $"{request.Method} {request.RequestUri}";
        diagnostics.WriteLine($"> {line}");
        WriteHeaders("> ", request.Headers);
        WriteHeaders("> ", request.Content?.Headers);

        var startedAt = Stopwatch.GetTimestamp();
        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            diagnostics.WriteLine($"! {line} falló ({exception.HttpRequestError}) tras {Elapsed(startedAt)}: {exception.Message}");
            throw;
        }
        // Aquí no se distingue el timeout de HttpClient de un Ctrl+C: ambos cancelan el mismo token.
        catch (OperationCanceledException)
        {
            diagnostics.WriteLine($"! {line} sin respuesta tras {Elapsed(startedAt)} (tiempo agotado o cancelado)");
            throw;
        }

        diagnostics.WriteLine($"< {(int)response.StatusCode} {response.ReasonPhrase} ({Elapsed(startedAt)})");
        WriteHeaders("< ", response.Headers);
        WriteHeaders("< ", response.Content.Headers);
        return response;
    }

    private static string Elapsed(long startedAt) => $"{Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds:0} ms";

    private void WriteHeaders(string prefix, HttpHeaders? headers)
    {
        if (headers is null)
        {
            return;
        }

        foreach (var (name, values) in headers)
        {
            if (!CredentialHeaders.Contains(name))
            {
                diagnostics.WriteLine($"{prefix}{name}: {string.Join(", ", values)}");
            }
        }
    }
}
