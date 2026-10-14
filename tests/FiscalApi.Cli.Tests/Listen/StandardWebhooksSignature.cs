using System.Security.Cryptography;
using System.Text;

namespace FiscalApi.Cli.Tests.Listen;

/// <summary>
/// La firma de Standard Webhooks: <c>v1,</c> + HMAC-SHA256 en base64 sobre <c>{id}.{timestamp}.{body}</c>, con la
/// llave que va en base64 después de <c>whsec_</c>. El cuerpo entra en bytes: así la verificación prueba que el reenvío
/// no cambió ni un byte.
/// </summary>
internal static class StandardWebhooksSignature
{
    public static string Sign(string id, string timestamp, byte[] body)
    {
        var key = Convert.FromBase64String(FakeListenerApi.Secret["whsec_".Length..]);
        var content = Encoding.UTF8.GetBytes($"{id}.{timestamp}.").Concat(body).ToArray();
        return $"v1,{Convert.ToBase64String(HMACSHA256.HashData(key, content))}";
    }
}
