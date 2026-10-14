using System.Net;

namespace FiscalApi.Cli.Http;

/// <summary>Los códigos HTTP que otro intento puede resolver: 408, 429 y 5xx. Un 4xx distinto nunca.</summary>
public static class TransientHttpStatus
{
    public static bool Is(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)statusCode >= 500;
}
