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

    /// <summary>
    /// CRED_MAX_CREDENTIAL_BLOB_SIZE de Credential Manager (5 * 512 bytes), el límite más chico de los tres almacenes.
    /// El escape de JSON puede volver un carácter en seis bytes (<c>"</c> → <c>"</c>), así que se prueba el peor.
    /// </summary>
    [Fact]
    public void Longest_credentials_fit_the_smallest_store_limit_even_with_json_escapes()
    {
        const int SmallestStoreLimit = 5 * 512;

        for (var character = '!'; character <= '~'; character++)
        {
            var longest = new string(character, ApiCredentials.MaxLength);

            var secret = StoredCredentialsFormat.Encode(ApiCredentials.Create(longest, longest));

            Assert.True(secret.Length <= SmallestStoreLimit, $"'{character}': {secret.Length} bytes");
        }
    }

    [Fact]
    public void Credentials_longer_than_the_maximum_are_rejected()
    {
        var tooLong = new string('a', ApiCredentials.MaxLength + 1);

        Assert.Throws<FiscalApi.Cli.Errors.UsageException>(() => ApiCredentials.Create(tooLong, "tenant"));
        Assert.Throws<FiscalApi.Cli.Errors.UsageException>(() => ApiCredentials.Create("sk_test", tooLong));
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
