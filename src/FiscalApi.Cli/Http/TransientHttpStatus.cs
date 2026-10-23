using System.Net;

namespace FiscalApi.Cli.Http;

public static class TransientHttpStatus
{
    public static bool Is(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)statusCode >= 500;
}
