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
        "Cookie"
    };

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        diagnostics.WriteLine($"> {request.Method} {request.RequestUri}");
        WriteHeaders("> ", request.Headers);

        var startedAt = Stopwatch.GetTimestamp();
        var response = await base.SendAsync(request, cancellationToken);

        var elapsed = Stopwatch.GetElapsedTime(startedAt);
        diagnostics.WriteLine($"< {(int)response.StatusCode} {response.ReasonPhrase} ({elapsed.TotalMilliseconds:0} ms)");
        WriteHeaders("< ", response.Headers);
        return response;
    }

    private void WriteHeaders(string prefix, HttpHeaders headers)
    {
        foreach (var (name, values) in headers)
        {
            if (!CredentialHeaders.Contains(name))
            {
                diagnostics.WriteLine($"{prefix}{name}: {string.Join(", ", values)}");
            }
        }
    }
}
