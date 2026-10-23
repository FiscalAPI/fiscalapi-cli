using System.Text.Json.Serialization;

namespace FiscalApi.Cli.Updates;

internal sealed record GitHubRelease(string? TagName, bool Draft, bool Prerelease);

internal sealed record UpdateCheckState(DateTimeOffset LastCheckedAt);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(GitHubRelease))]
[JsonSerializable(typeof(UpdateCheckState))]
internal sealed partial class UpdateCheckJsonContext : JsonSerializerContext;
