using System.Net;
using FiscalApi.Cli.Errors;

namespace FiscalApi.Cli.Tests.Http;

/// <summary>
/// Traducción del envelope <c>ApiResponse</c> de fapi-back a <see cref="ApiException"/>.
/// </summary>
public sealed class FiscalApiClientErrorTests
{
    private static readonly int AllAttempts = FiscalApiClientBuilder.NoDelayRetryPolicy.MaxAttempts;

    [Fact]
    public async Task Success_returns_the_data_of_the_envelope()
    {
        var handler = new StubHttpMessageHandler()
            .RespondJson(HttpStatusCode.OK, """{"succeeded":true,"message":"","details":"","data":{"name":"ok"}}""");

        var data = await GetAsync(handler);

        Assert.Equal(new ProbeData("ok"), data);
    }

    [Fact]
    public async Task Unauthorized_carries_the_api_message_and_the_login_hint()
    {
        var handler = new StubHttpMessageHandler()
            .RespondJson(HttpStatusCode.Unauthorized,
                """{"succeeded":false,"message":"No autorizado","details":"La API key no es válida","traceIdentifier":"0HN7:00000001"}""");

        var error = await Assert.ThrowsAsync<ApiException>(() => GetAsync(handler));

        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
        Assert.Equal("No autorizado", error.Message);
        Assert.Equal("La API key no es válida", error.Details);
        Assert.Equal("0HN7:00000001", error.TraceIdentifier);
        Assert.Contains("fiscalapi login", error.Hint);
        Assert.Contains("FISCALAPI_API_KEY", error.Hint);
    }

    [Fact]
    public async Task Not_found_has_no_hint()
    {
        var handler = new StubHttpMessageHandler()
            .RespondJson(HttpStatusCode.NotFound, """{"succeeded":false,"message":"Recurso no encontrado","details":""}""");

        var error = await Assert.ThrowsAsync<ApiException>(() => GetAsync(handler));

        Assert.Equal(HttpStatusCode.NotFound, error.StatusCode);
        Assert.Equal("Recurso no encontrado", error.Message);
        Assert.Null(error.Hint);
    }

    [Fact]
    public async Task Server_error_shows_the_trace_identifier_for_support()
    {
        // Un 500 en un GET se reintenta: cada intento recibe la misma respuesta.
        var handler = new StubHttpMessageHandler()
            .RespondJson(HttpStatusCode.InternalServerError,
                """{"succeeded":false,"message":"Error interno","details":"NullReferenceException","traceIdentifier":"0HN7:0000002A"}""",
                times: AllAttempts);

        var error = await Assert.ThrowsAsync<ApiException>(() => GetAsync(handler));
        var rendered = Render(error);

        Assert.Equal(HttpStatusCode.InternalServerError, error.StatusCode);
        Assert.Contains("Error interno", rendered);
        Assert.Contains("NullReferenceException", rendered);
        Assert.Contains("500", rendered);
        Assert.Contains("0HN7:0000002A", rendered);
    }

    [Fact]
    public async Task Status_code_comes_from_the_response_even_when_the_envelope_omits_it()
    {
        var handler = new StubHttpMessageHandler()
            .RespondJson(HttpStatusCode.Conflict, """{"succeeded":false,"message":"Ya existe","details":"","httpStatusCode":0}""");

        var error = await Assert.ThrowsAsync<ApiException>(() => GetAsync(handler));

        Assert.Equal(HttpStatusCode.Conflict, error.StatusCode);
    }

    [Fact]
    public async Task Body_that_is_not_json_is_reported_as_an_unexpected_response()
    {
        var handler = new StubHttpMessageHandler()
            .RespondHtml(HttpStatusCode.BadGateway, "<html><body>502 Bad Gateway</body></html>", times: AllAttempts);

        var error = await Assert.ThrowsAsync<ApiException>(() => GetAsync(handler));

        Assert.Equal(HttpStatusCode.BadGateway, error.StatusCode);
        Assert.Equal("Respuesta inesperada (502).", error.Message);
        Assert.Null(error.TraceIdentifier);
    }

    [Fact]
    public async Task Success_status_with_succeeded_false_is_an_api_error()
    {
        var handler = new StubHttpMessageHandler()
            .RespondJson(HttpStatusCode.OK, """{"succeeded":false,"message":"Operación rechazada","details":""}""");

        var error = await Assert.ThrowsAsync<ApiException>(() => GetAsync(handler));

        Assert.Equal(HttpStatusCode.OK, error.StatusCode);
        Assert.Equal("Operación rechazada", error.Message);
    }

    [Fact]
    public async Task Error_without_message_still_reports_the_status_code()
    {
        var handler = new StubHttpMessageHandler()
            .RespondJson(HttpStatusCode.Forbidden, """{"succeeded":false}""");

        var error = await Assert.ThrowsAsync<ApiException>(() => GetAsync(handler));

        Assert.Equal("La API respondió 403 sin mensaje.", error.Message);
    }

    [Fact]
    public async Task Network_failure_is_a_network_exception_with_exit_code_1()
    {
        var handler = new StubHttpMessageHandler().FailWithNetworkError(times: AllAttempts);

        var error = await Assert.ThrowsAsync<NetworkException>(() => GetAsync(handler));

        Assert.Equal(ExitCodes.Failure, error.ExitCode);
        Assert.Contains(FiscalApiClientBuilder.BaseAddress.Host, error.Message);
    }

    private static Task<ProbeData?> GetAsync(StubHttpMessageHandler handler) =>
        FiscalApiClientBuilder.Create(handler)
            .GetAsync("api/v4/probe", TestJsonContext.Default.ApiResponseProbeData, CancellationToken.None);

    private static string Render(CliException error)
    {
        using var writer = new StringWriter();
        error.WriteTo(writer);
        return writer.ToString();
    }
}
