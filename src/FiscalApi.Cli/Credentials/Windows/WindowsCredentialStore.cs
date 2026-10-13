using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using FiscalApi.Cli.Errors;

namespace FiscalApi.Cli.Credentials.Windows;

/// <summary>
/// Windows Credential Manager: una credencial genérica (<c>CRED_TYPE_GENERIC</c>) por host, con nombre
/// <c>{servicio}:{host}</c> (p. ej. <c>fiscalapi-cli:test.fiscalapi.com</c>). Se ve y se borra también desde
/// "Administrador de credenciales" o con <c>cmdkey /delete:fiscalapi-cli:test.fiscalapi.com</c>.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed unsafe class WindowsCredentialStore(string service) : ICredentialStore
{
    private const uint CredTypeGeneric = 1;

    // Solo en este equipo y para esta persona. CRED_PERSIST_ENTERPRISE viajaría con un perfil móvil a otros equipos.
    private const uint CredPersistLocalMachine = 2;

    private const int ErrorNotFound = 1168;

    // Sesiones de red o de servicio sin conjunto de credenciales: no hay almacén que usar.
    private const int ErrorNoSuchLogonSession = 1312;

    public ApiCredentials? Read(string host)
    {
        AdvApi32.CREDENTIALW* credential;
        if (!AdvApi32.CredReadW(TargetName(host), CredTypeGeneric, flags: 0, &credential))
        {
            var error = Marshal.GetLastPInvokeError();
            return error == ErrorNotFound ? null : throw Failure(error, "leer");
        }

        try
        {
            // El bloque es nuestro hasta CredFree: se borra el secreto antes de devolverlo al sistema.
            var secret = new Span<byte>(credential->CredentialBlob, checked((int)credential->CredentialBlobSize));
            try
            {
                return StoredCredentialsFormat.Decode(secret, host);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(secret);
            }
        }
        finally
        {
            AdvApi32.CredFree(credential);
        }
    }

    public void Save(string host, ApiCredentials credentials)
    {
        var secret = StoredCredentialsFormat.Encode(credentials);
        try
        {
            fixed (char* targetName = TargetName(host))
            fixed (byte* blob = secret)
            {
                var credential = new AdvApi32.CREDENTIALW
                {
                    Type = CredTypeGeneric,
                    TargetName = targetName,
                    CredentialBlobSize = (uint)secret.Length,
                    CredentialBlob = blob,
                    Persist = CredPersistLocalMachine
                };

                // CredWriteW crea la entrada o reemplaza la que tenga el mismo TargetName y Type.
                if (!AdvApi32.CredWriteW(&credential, flags: 0))
                {
                    throw Failure(Marshal.GetLastPInvokeError(), "guardar");
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    public bool Delete(string host)
    {
        if (AdvApi32.CredDeleteW(TargetName(host), CredTypeGeneric, flags: 0))
        {
            return true;
        }

        var error = Marshal.GetLastPInvokeError();
        return error == ErrorNotFound ? false : throw Failure(error, "borrar");
    }

    private string TargetName(string host) => $"{service}:{host}";

    private static CliException Failure(int error, string operation) => error == ErrorNoSuchLogonSession
        ? new CredentialStoreUnavailableException($"Credential Manager no tiene credenciales para esta sesión (Win32 {error}).")
        : new CredentialStoreException($"Credential Manager falló al {operation} la entrada (Win32 {error}).");
}
