using System.Text.Json;
using System.Text.Json.Serialization;
using FiscalApi.Cli.Http;

namespace FiscalApi.Cli.Tests.Http;

public sealed record ProbeData(string Name);

public sealed record ProbeRequest(string Name);

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(ApiResponse<ProbeData>))]
[JsonSerializable(typeof(ProbeRequest))]
public sealed partial class TestJsonContext : JsonSerializerContext;
