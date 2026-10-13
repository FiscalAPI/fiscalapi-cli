using System.Text.Json;

namespace FiscalApi.Cli.Updates;

/// <summary>
/// Archivo del usuario con la fecha de la última consulta de versión nueva. No guarda secretos: perderlo o
/// corromperlo solo adelanta la siguiente consulta.
/// </summary>
public sealed class UpdateCheckStateFile(string path)
{
    private const string DirectoryName = "fiscalapi-cli";
    private const string FileName = "update-check.json";

    public string Path { get; } = path;

    /// <summary>
    /// La ubicación idiomática de cada SO: <c>%LOCALAPPDATA%\fiscalapi-cli</c> en Windows,
    /// <c>~/Library/Application Support/fiscalapi-cli</c> en macOS (<see cref="Environment.SpecialFolder.LocalApplicationData"/>
    /// desde .NET 8) y <c>$XDG_STATE_HOME/fiscalapi-cli</c> en Linux. <c>null</c> si el usuario no tiene dónde guardarlo.
    /// </summary>
    public static UpdateCheckStateFile? ForCurrentUser(Func<string, string?> environment)
    {
        var directory = OperatingSystem.IsLinux() ? LinuxStateDirectory(environment) : LocalApplicationDataDirectory();
        return directory is null ? null : new UpdateCheckStateFile(System.IO.Path.Combine(directory, FileName));
    }

    /// <summary>
    /// XDG Base Directory: <c>$XDG_STATE_HOME</c> si es una ruta absoluta (la especificación pide ignorar las relativas)
    /// y si no <c>$HOME/.local/state</c>. .NET no tiene un <see cref="Environment.SpecialFolder"/> para él.
    /// </summary>
    internal static string? LinuxStateDirectory(Func<string, string?> environment)
    {
        var stateHome = environment("XDG_STATE_HOME");
        if (stateHome is not null && stateHome.StartsWith('/'))
        {
            return System.IO.Path.Combine(stateHome, DirectoryName);
        }

        var home = environment("HOME");
        return string.IsNullOrEmpty(home) ? null : System.IO.Path.Combine(home, ".local", "state", DirectoryName);
    }

    /// <summary>
    /// Toca consultar si nunca se consultó, si pasó el intervalo o si la fecha guardada está en el futuro (el reloj del
    /// equipo se atrasó). Un archivo que no se puede leer no permite respetar el intervalo, así que no toca.
    /// </summary>
    internal bool IsDue(DateTimeOffset now, TimeSpan interval)
    {
        UpdateCheckState? state;
        try
        {
            state = JsonSerializer.Deserialize(File.ReadAllBytes(Path), UpdateCheckJsonContext.Default.UpdateCheckState);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException or JsonException)
        {
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        return state is null || state.LastCheckedAt > now || now - state.LastCheckedAt >= interval;
    }

    /// <summary>
    /// Guarda la fecha de la consulta. Escribe un temporal y lo mueve encima del archivo: otro proceso del CLI nunca lee
    /// un archivo a medio escribir. <c>false</c> si no se pudo guardar (carpeta de solo lectura, disco lleno…).
    /// </summary>
    internal bool TryRecordCheck(DateTimeOffset now)
    {
        var temporary = $"{Path}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(new UpdateCheckState(now), UpdateCheckJsonContext.Default.UpdateCheckState));
            File.Move(temporary, Path, overwrite: true);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            DeleteQuietly(temporary);
            return false;
        }
    }

    private static string? LocalApplicationDataDirectory()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
        return string.IsNullOrEmpty(root) ? null : System.IO.Path.Combine(root, DirectoryName);
    }

    private static void DeleteQuietly(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Un temporal huérfano no rompe nada: el siguiente guardado usa otro nombre.
        }
    }
}
