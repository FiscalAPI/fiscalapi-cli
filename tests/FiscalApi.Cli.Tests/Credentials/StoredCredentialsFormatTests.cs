using System.Text;
using FiscalApi.Cli.Credentials;

namespace FiscalApi.Cli.Tests.Credentials;

/// <summary>
/// El secreto de cada entrada del almacén: JSON UTF-8 con las dos llaves. Una entrada que no es de este CLI o se
/// editó a mano es un error del almacén que pide volver a hacer login, sin repetir lo que trae.
/// </summary>
public sealed class StoredCredentialsFormatTests
{
    private const string Host = "test.fiscalapi.com";

    [Fact]
    public void Encoded_secret_is_json_with_both_keys_and_decodes_back()
    {
        var credentials = ApiCredentials.Create("sk_test_formato", "tenant-formato");

        var secret = StoredCredentialsFormat.Encode(credentials);

        Assert.Equal("""{"apiKey":"sk_test_formato","tenantKey":"tenant-formato"}""", Encoding.UTF8.GetString(secret));
        Assert.Equal(credentials, StoredCredentialsFormat.Decode(secret, Host));
    }

    [Theory]
    [InlineData("contraseña-escrita-a-mano")]
    [InlineData("""{"apiKey":"sk_test_sin-tenant"}""")]
    [InlineData("""{"apiKey":"sk_test_con espacio","tenantKey":"tenant-valido"}""")]
    [InlineData("null")]
    public void Entry_that_cannot_be_read_asks_to_log_in_again_without_echoing_it(string stored)
    {
        var error = Assert.Throws<CredentialStoreException>(() => StoredCredentialsFormat.Decode(Encoding.UTF8.GetBytes(stored), Host));

        Assert.Equal(ExitCodes.Failure, error.ExitCode);
        Assert.Contains(Host, error.Message);
        Assert.Contains("fiscalapi login", error.Message);
        Assert.DoesNotContain("contraseña", error.Message);
        Assert.DoesNotContain("sk_test", error.Message);
    }
}
