using System.Net.Http.Headers;
using System.Text;

namespace FiscalApi.Cli.Listen;

public sealed class HttpForwardTarget : IDeliveryTarget, IDisposable
{
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

    public static HttpForwardTarget Create(
        Uri forwardTo,
        IReadOnlyList<KeyValuePair<string, string>> extraHeaders,
        bool skipVerify,
        TimeSpan timeout,
        TimeProvider timeProvider)
    {
        var handler = new SocketsHttpHandler { AllowAutoRedirect = false };
        if (skipVerify)
        {
            handler.SslOptions.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
        }

        var client = new HttpClient(handler, disposeHandler: true) { Timeout = timeout };
        return new HttpForwardTarget(client, forwardTo, extraHeaders, timeProvider);
    }

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
        var content = new ByteArrayContent(BodyEncoding.GetBytes(delivery.Body));
        content.Headers.ContentType = JsonContentType;
        var request = new HttpRequestMessage(HttpMethod.Post, _forwardTo) { Content = content };
        foreach (var (name, value) in delivery.Headers.Concat(_extraHeaders))
        {
            request.Headers.Add(name, value);
        }

        return request;
    }

    private static DeliveryOutcome Outcome(int statusCode, int latencyMs) =>
        statusCode is >= 100 and <= 599
            ? DeliveryOutcome.Responded(statusCode, latencyMs)
            : DeliveryOutcome.Failed(InvalidResponseError, latencyMs);

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
