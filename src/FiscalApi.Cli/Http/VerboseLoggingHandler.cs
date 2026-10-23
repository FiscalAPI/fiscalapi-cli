using System.Diagnostics;
using System.Net.Http.Headers;

namespace FiscalApi.Cli.Http;

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
            diagnostics.WriteLine($"! {line} failed ({exception.HttpRequestError}) after {Elapsed(startedAt)}: {exception.Message}");
            throw;
        }
        catch (OperationCanceledException)
        {
            diagnostics.WriteLine($"! {line} got no response after {Elapsed(startedAt)} (timed out or canceled)");
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
