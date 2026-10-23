using System.Security.Cryptography;
using System.Text;

namespace FiscalApi.Cli.Tests.Listen;

internal static class StandardWebhooksSignature
{
    public static string Sign(string id, string timestamp, byte[] body)
    {
        var key = Convert.FromBase64String(FakeListenerApi.Secret["whsec_".Length..]);
        var content = Encoding.UTF8.GetBytes($"{id}.{timestamp}.").Concat(body).ToArray();
        return $"v1,{Convert.ToBase64String(HMACSHA256.HashData(key, content))}";
    }
}
