using System.Net.Http.Headers;
using System.Text;

namespace FiscalApi.Cli.Listen;

/// <summary>
/// Reenvía cada entrega a <c>--forward-to</c> con su propio <see cref="HttpClient"/>, nunca el de la API: los headers de
/// credenciales (<c>X-API-KEY</c>, <c>X-TENANT-KEY</c>) no tienen cómo llegar al receptor.
/// </summary>
public sealed class HttpForwardTarget : IDeliveryTarget, IDisposable
{
    // UTF-8 sin BOM, como el envío de fapi-back (WebhookHttpSender): la firma es sobre los bytes exactos del cuerpo.
    private static readonly UTF8Encoding BodyEncoding = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly MediaTypeHeaderValue JsonContentType = new("application/json") { CharSet = "utf-8" };

    private const string TimeoutError = "Timeout";
    private const string InvalidResponseError = "InvalidResponse";

    private readonly HttpClient _client;
    private readonly Uri _forwardTo;
    private readonly IReadOnlyList<KeyValuePair<string, string>> _extraHeaders;
    private readonly TimeProvider _timeProvider;

    private HttpForwardTarget(
        HttpClient client, Uri forwardTo, IReadOnlyList<KeyValuePair<string, string>> extraHeaders, TimeProvider timeProvider)
    {
        _client = client;
        _forwardTo = forwardTo;
        _extraHeaders = extraHeaders;
        _timeProvider = timeProvider;
    }

    /// <param name="forwardTo">La URL de <c>--forward-to</c>.</param>
    /// <param name="extraHeaders">Los de <c>--headers</c>, ya validados; nunca uno de los firmados.</param>
    /// <param name="skipVerify"><c>--skip-verify</c>: acepta cualquier certificado, solo en este cliente.</param>
    /// <param name="timeout">El plazo de cada reenvío.</param>
    /// <param name="timeProvider">Mide la latencia que lleva el acuse.</param>
    public static HttpForwardTarget Create(
        Uri forwardTo,
        IReadOnlyList<KeyValuePair<string, string>> extraHeaders,
        bool skipVerify,
        TimeSpan timeout,
        TimeProvider timeProvider)
    {
        // Un handler propio y no el de la API: --skip-verify relaja TLS hacia el receptor local (un certificado de
        // desarrollo) y nunca hacia FiscalAPI. Sin redirecciones, como el envío de fapi-back.
        var handler = new SocketsHttpHandler { AllowAutoRedirect = false };
        if (skipVerify)
        {
            handler.SslOptions.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
        }

        var client = new HttpClient(handler, disposeHandler: true) { Timeout = timeout };
        return new HttpForwardTarget(client, forwardTo, extraHeaders, timeProvider);
    }

    /// <summary>
    /// Reenvía la entrega y responde lo que pasó; un receptor que no responde no es una excepción sino un resultado que se
    /// acusa. Solo se leen los headers de la respuesta: el cuerpo no se usa.
    /// </summary>
    /// <exception cref="OperationCanceledException">Solo si se canceló <paramref name="cancellationToken"/> (Ctrl+C).</exception>
    public async Task<DeliveryOutcome> DeliverAsync(CliDelivery delivery, CancellationToken cancellationToken)
    {
        var startedAt = _timeProvider.GetTimestamp();
        try
        {
            using var request = CreateRequest(delivery);
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            return Outcome((int)response.StatusCode, ElapsedMs(startedAt));
        }
        // El timeout de HttpClient llega como cancelación sin que nadie haya cancelado el token de quien llama.
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return DeliveryOutcome.Failed(TimeoutError, ElapsedMs(startedAt));
        }
        catch (HttpRequestException exception)
        {
            return DeliveryOutcome.Failed(Classify(exception.HttpRequestError), ElapsedMs(startedAt));
        }
    }

    public void Dispose() => _client.Dispose();

    private HttpRequestMessage CreateRequest(CliDelivery delivery)
    {
        // Los bytes del cuerpo tal como se firmaron: sin re-serializar ni normalizar.
        var content = new ByteArrayContent(BodyEncoding.GetBytes(delivery.Body));
        content.Headers.ContentType = JsonContentType;
        var request = new HttpRequestMessage(HttpMethod.Post, _forwardTo) { Content = content };
        foreach (var (name, value) in delivery.Headers.Concat(_extraHeaders))
        {
            request.Headers.Add(name, value);
        }

        return request;
    }

    // HttpClient acepta cualquier código de tres cifras; la API solo acusa 100..599 (AckWebhookCliDeliveryCommandValidator).
    // Otro código no es HTTP válido, como lo clasifica el envío de fapi-back.
    private static DeliveryOutcome Outcome(int statusCode, int latencyMs) =>
        statusCode is >= 100 and <= 599
            ? DeliveryOutcome.Responded(statusCode, latencyMs)
            : DeliveryOutcome.Failed(InvalidResponseError, latencyMs);

    // Los mismos nombres que WebhookAttemptError de fapi-back: el log de entregas de la escucha se lee igual que el de un
    // endpoint con URL.
    private static string Classify(HttpRequestError error) => error switch
    {
        HttpRequestError.SecureConnectionError => "TlsFailed",
        HttpRequestError.InvalidResponse or HttpRequestError.ResponseEnded or HttpRequestError.ConfigurationLimitExceeded
            => InvalidResponseError,
        _ => "ConnectionFailed"
    };

    private int ElapsedMs(long startedAt) =>
        (int)Math.Min(_timeProvider.GetElapsedTime(startedAt).TotalMilliseconds, int.MaxValue);
}
