namespace FiscalApi.Cli.Tests.Http;

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
