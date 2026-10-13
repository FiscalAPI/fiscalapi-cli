namespace FiscalApi.Cli.Credentials;

/// <summary>
/// Almacén de credenciales del SO, con una entrada por host de API (<see cref="ApiEnvironment.Host"/>), así las
/// credenciales de test, live y el back local no se pisan (D-25). Nunca hay un archivo plano ni otro respaldo:
/// si el SO no tiene almacén, cada operación lanza <see cref="CredentialStoreUnavailableException"/>; si lo tiene
/// pero está bloqueado o falla, <see cref="CredentialStoreException"/>.
/// </summary>
public interface ICredentialStore
{
    /// <summary>
    /// Comprueba que el almacén se puede usar consultando la entrada del host, sin leer ni decodificar su secreto:
    /// <c>login</c> lo hace antes de pedir las llaves, para no pedirlas si no se van a poder guardar.
    /// </summary>
    void EnsureAvailable(string host);

    /// <returns>Las credenciales guardadas para el host, o <c>null</c> si no hay.</returns>
    ApiCredentials? Read(string host);

    /// <summary>Crea la entrada del host o reemplaza la que ya existía.</summary>
    void Save(string host, ApiCredentials credentials);

    /// <returns><c>true</c> si había una entrada y se borró; <c>false</c> si no había nada que borrar.</returns>
    bool Delete(string host);
}
