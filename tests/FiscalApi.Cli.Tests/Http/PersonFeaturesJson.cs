namespace FiscalApi.Cli.Tests.Http;

/// <summary>
/// Respuestas de <c>GET /api/v4/me/features</c> con la forma de <c>PersonFeaturesDto</c> de fapi-back
/// (<c>WebApi.Libs/Application/AppFeatures/Me/PersonFeaturesDto.cs</c>), en camelCase.
/// </summary>
internal static class PersonFeaturesJson
{
    public const string WithWebhooksManage = """
        {"succeeded":true,"message":"","details":"","data":{
          "isOwner":true,"roles":["Owner"],
          "permissions":["Invoices.Create","Webhooks.Manage"],
          "denied":{},
          "features":{"webhooks":{"values":{}}},
          "subscription":{"status":"active","isAvailable":true},
          "seatAssignments":[]}}
        """;

    /// <summary>Un tenant sin el feature de webhooks: el permiso aparece en <c>denied</c>, no en <c>permissions</c>.</summary>
    public const string WithoutWebhooksManage = """
        {"succeeded":true,"message":"","details":"","data":{
          "isOwner":false,"roles":["Member"],
          "permissions":["Invoices.Create"],
          "denied":{"Webhooks.Manage":{"missingFeatures":["webhooks"]}},
          "features":{},
          "subscription":{"status":"active","isAvailable":true},
          "seatAssignments":[]}}
        """;
}
