using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using FiscalApi.Cli.Errors;

namespace FiscalApi.Cli.Http;

/// <summary>
/// Cliente de la API de FiscalAPI para los comandos. Devuelve el <c>data</c> del envelope y convierte cualquier
/// otra salida en <see cref="ApiException"/> o <see cref="NetworkException"/>. El JSON pasa siempre por un
/// <see cref="JsonTypeInfo{T}"/> generado, nunca por reflexión.
/// </summary>
public sealed class FiscalApiClient(HttpClient httpClient, RetryPolicy retryPolicy)
{
    /// <summary>Un GET es idempotente: se reintenta ante errores transitorios.</summary>
    public async Task<T?> GetAsync<T>(
        string path, JsonTypeInfo<ApiResponse<T>> responseType, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            () => retryPolicy.SendAsync(httpClient, () => new HttpRequestMessage(HttpMethod.Get, path), cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return await ReadDataAsync(response, responseType, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Un POST no se reintenta: repetirlo podría crear el recurso dos veces.</summary>
    public async Task<TResponse?> PostAsync<TRequest, TResponse>(
        string path,
        TRequest body,
        JsonTypeInfo<TRequest> requestType,
        JsonTypeInfo<ApiResponse<TResponse>> responseType,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body, requestType)
        };
        using var response = await SendAsync(() => httpClient.SendAsync(request, cancellationToken), cancellationToken)
            .ConfigureAwait(false);
        return await ReadDataAsync(response, responseType, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync(
        Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
    {
        try
        {
            return await send().ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new NetworkException($"No se pudo conectar con {httpClient.BaseAddress}: {exception.Message}", exception);
        }
        // HttpClient reporta su timeout como TaskCanceledException sin que el token de quien llama se haya
        // cancelado. Si el token sí se canceló (Ctrl+C), la excepción sigue su camino hasta el código 130.
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new NetworkException(
                $"{httpClient.BaseAddress} no respondió en {httpClient.Timeout.TotalSeconds:0} s.", exception);
        }
    }

    private static async Task<T?> ReadDataAsync<T>(
        HttpResponseMessage response, JsonTypeInfo<ApiResponse<T>> responseType, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw await ToApiExceptionAsync(response, cancellationToken).ConfigureAwait(false);
        }

        // En un 2xx el contrato es nuestro: un JsonException aquí es un defecto del CLI y sube tal cual.
        var envelope = await response.Content.ReadFromJsonAsync(responseType, cancellationToken).ConfigureAwait(false)
            ?? throw ApiException.UnexpectedResponse(response.StatusCode);
        return envelope.Succeeded ? envelope.Data : throw ApiException.FromEnvelope(response.StatusCode, envelope);
    }

    /// <summary>
    /// Un error se lee sin <c>data</c>, cuya forma no tiene por qué coincidir con la del éxito. Un cuerpo que
    /// no es JSON (el 502/503 en HTML de nginx, un 3xx sin cuerpo) es una respuesta inesperada, no un fallo del CLI.
    /// </summary>
    private static async Task<ApiException> ToApiExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var envelope = await response.Content
                .ReadFromJsonAsync(FiscalApiJsonContext.Default.ApiResponse, cancellationToken).ConfigureAwait(false);
            return envelope is null
                ? ApiException.UnexpectedResponse(response.StatusCode)
                : ApiException.FromEnvelope(response.StatusCode, envelope);
        }
        catch (JsonException)
        {
            return ApiException.UnexpectedResponse(response.StatusCode);
        }
    }
}
