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
            cancellationToken);
        return await ReadDataAsync(response, responseType, cancellationToken);
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
        using var response = await SendAsync(() => httpClient.SendAsync(request, cancellationToken), cancellationToken);
        return await ReadDataAsync(response, responseType, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
    {
        try
        {
            return await send();
        }
        catch (HttpRequestException exception)
        {
            throw new NetworkException($"No se pudo conectar con {httpClient.BaseAddress}: {exception.Message}", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new NetworkException(
                $"{httpClient.BaseAddress} no respondió en {httpClient.Timeout.TotalSeconds:0} s.", exception);
        }
    }

    private static async Task<T?> ReadDataAsync<T>(
        HttpResponseMessage response, JsonTypeInfo<ApiResponse<T>> responseType, CancellationToken cancellationToken)
    {
        // Un error se lee sin "data": su forma no tiene por qué coincidir con T y no debe ocultar el mensaje.
        if (!response.IsSuccessStatusCode)
        {
            var error = await TryReadAsync(response, FiscalApiJsonContext.Default.ApiResponse, cancellationToken);
            throw error is null
                ? ApiException.UnexpectedResponse(response.StatusCode)
                : ApiException.FromEnvelope(response.StatusCode, error);
        }

        var envelope = await TryReadAsync(response, responseType, cancellationToken)
            ?? throw ApiException.UnexpectedResponse(response.StatusCode);
        return envelope.Succeeded ? envelope.Data : throw ApiException.FromEnvelope(response.StatusCode, envelope);
    }

    // Un 502/503 de nginx trae HTML o nada: eso es una respuesta inesperada, no un fallo del CLI.
    private static async Task<TEnvelope?> TryReadAsync<TEnvelope>(
        HttpResponseMessage response, JsonTypeInfo<TEnvelope> envelopeType, CancellationToken cancellationToken)
        where TEnvelope : ApiResponse
    {
        try
        {
            return await response.Content.ReadFromJsonAsync(envelopeType, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
