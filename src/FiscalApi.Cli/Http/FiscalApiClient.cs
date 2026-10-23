using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using FiscalApi.Cli.Errors;

namespace FiscalApi.Cli.Http;

public sealed class FiscalApiClient(HttpClient httpClient, RetryPolicy retryPolicy)
{
    public async Task<T?> GetAsync<T>(
        string path, JsonTypeInfo<ApiResponse<T>> responseType, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            () => retryPolicy.SendAsync(httpClient, () => new HttpRequestMessage(HttpMethod.Get, path), cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return await ReadDataAsync(response, responseType, cancellationToken).ConfigureAwait(false);
    }

    public Task<TResponse?> PostAsync<TRequest, TResponse>(
        string path,
        TRequest body,
        JsonTypeInfo<TRequest> requestType,
        JsonTypeInfo<ApiResponse<TResponse>> responseType,
        CancellationToken cancellationToken) =>
        SendOnceAsync(HttpMethod.Post, path, JsonContent.Create(body, requestType), responseType, cancellationToken);

    public Task<TResponse?> PostAsync<TResponse>(
        string path, JsonTypeInfo<ApiResponse<TResponse>> responseType, CancellationToken cancellationToken) =>
        SendOnceAsync(HttpMethod.Post, path, content: null, responseType, cancellationToken);

    public Task<TResponse?> PutAsync<TRequest, TResponse>(
        string path,
        TRequest body,
        JsonTypeInfo<TRequest> requestType,
        JsonTypeInfo<ApiResponse<TResponse>> responseType,
        CancellationToken cancellationToken) =>
        SendOnceAsync(HttpMethod.Put, path, JsonContent.Create(body, requestType), responseType, cancellationToken);

    public Task<TResponse?> DeleteAsync<TResponse>(
        string path, JsonTypeInfo<ApiResponse<TResponse>> responseType, CancellationToken cancellationToken) =>
        SendOnceAsync(HttpMethod.Delete, path, content: null, responseType, cancellationToken);

    private async Task<TResponse?> SendOnceAsync<TResponse>(
        HttpMethod method,
        string path,
        HttpContent? content,
        JsonTypeInfo<ApiResponse<TResponse>> responseType,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
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
            throw new NetworkException($"Could not connect to {httpClient.BaseAddress}: {exception.Message}", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new NetworkException(
                $"{httpClient.BaseAddress} did not respond within {httpClient.Timeout.TotalSeconds:0} s.", exception);
        }
    }

    private static async Task<T?> ReadDataAsync<T>(
        HttpResponseMessage response, JsonTypeInfo<ApiResponse<T>> responseType, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw await ToApiExceptionAsync(response, cancellationToken).ConfigureAwait(false);
        }

        var envelope = await response.Content.ReadFromJsonAsync(responseType, cancellationToken).ConfigureAwait(false)
            ?? throw ApiException.UnexpectedResponse(response.StatusCode);
        return envelope.Succeeded ? envelope.Data : throw ApiException.FromEnvelope(response.StatusCode, envelope);
    }

    private static async Task<ApiException> ToApiExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var envelope = await response.Content
                .ReadFromJsonAsync(FiscalApiJsonContext.Default.ApiResponseJsonElement, cancellationToken).ConfigureAwait(false);
            return envelope is null
                ? ApiException.UnexpectedResponse(response.StatusCode)
                : ApiException.FromEnvelope(response.StatusCode, envelope, ValidationFailures.MessagesIn(envelope.Data));
        }
        catch (JsonException)
        {
            return ApiException.UnexpectedResponse(response.StatusCode);
        }
    }
}
