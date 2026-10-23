using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using FiscalApi.Cli.Errors;

namespace FiscalApi.Cli.Credentials.Windows;

[SupportedOSPlatform("windows")]
internal sealed unsafe class WindowsCredentialStore(string service) : ICredentialStore
{
    private const string StoreName = "Windows Credential Manager";

    private const uint CredTypeGeneric = 1;

    private const uint CredPersistLocalMachine = 2;

    private const int ErrorNotFound = 1168;

    private const int ErrorNoSuchLogonSession = 1312;

    private delegate T SecretDecoder<out T>(ReadOnlySpan<byte> secret, string host);

    public void EnsureAvailable(string host) => ReadSecret(host, "check", static (_, _) => true);

    public ApiCredentials? Read(string host) => ReadSecret<ApiCredentials>(host, "read", StoredCredentialsFormat.Decode);

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

                if (!AdvApi32.CredWriteW(&credential, flags: 0))
                {
                    throw Failure(Marshal.GetLastPInvokeError(), "save");
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
        return error == ErrorNotFound ? false : throw Failure(error, "delete");
    }

    private T? ReadSecret<T>(string host, string operation, SecretDecoder<T> decode)
    {
        AdvApi32.CREDENTIALW* credential;
        if (!AdvApi32.CredReadW(TargetName(host), CredTypeGeneric, flags: 0, &credential))
        {
            var error = Marshal.GetLastPInvokeError();
            return error == ErrorNotFound ? default : throw Failure(error, operation);
        }

        try
        {
            var secret = new Span<byte>(credential->CredentialBlob, checked((int)credential->CredentialBlobSize));
            try
            {
                return decode(secret, host);
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

    private string TargetName(string host) => $"{service}:{host}";

    private static CliException Failure(int error, string operation) => error == ErrorNoSuchLogonSession
        ? new CredentialStoreUnavailableException($"{StoreName}: not available in this logon session (Win32 error {error}).")
        : CredentialStoreException.Failed(operation, $"{StoreName}: Win32 error {error}");
}
