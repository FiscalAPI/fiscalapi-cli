using System.Text.Json.Serialization;

namespace FiscalApi.Cli.Updates;

/// <summary>
/// Lo que el aviso usa de la respuesta de <c>GET /repos/{owner}/{repo}/releases/latest</c>
/// (docs.github.com/rest/releases/releases#get-the-latest-release); lo demás se ignora.
/// </summary>
internal sealed record GitHubRelease(string? TagName, bool Draft, bool Prerelease);

/// <summary>Contenido del archivo de estado: solo la fecha de la última consulta, nada secreto.</summary>
internal sealed record UpdateCheckState(DateTimeOffset LastCheckedAt);

/// <summary>JSON generado en compilación, como el resto del CLI: la reflexión no funciona en Native AOT.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(GitHubRelease))]
[JsonSerializable(typeof(UpdateCheckState))]
internal sealed partial class UpdateCheckJsonContext : JsonSerializerContext;
