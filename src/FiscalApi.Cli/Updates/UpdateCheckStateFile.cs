using System.Text.Json;

namespace FiscalApi.Cli.Updates;

public sealed class UpdateCheckStateFile(string path)
{
    private const string DirectoryName = "fiscalapi-cli";
    private const string FileName = "update-check.json";

    private readonly string _directory = ParentDirectoryOf(path);

    public string Path { get; } = path;

    public static UpdateCheckStateFile? ForCurrentUser(Func<string, string?> environment)
    {
        var directory = OperatingSystem.IsLinux() ? LinuxStateDirectory(environment) : LocalApplicationDataDirectory();
        return directory is null ? null : new UpdateCheckStateFile(System.IO.Path.Combine(directory, FileName));
    }

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

    internal bool TryRecordCheck(DateTimeOffset now)
    {
        var temporary = $"{Path}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(_directory);
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

    private static string ParentDirectoryOf(string path) =>
        System.IO.Path.GetDirectoryName(path) is { Length: > 0 } directory
            ? directory
            : throw new ArgumentException($"The update check state file needs a parent directory: '{path}'.", nameof(path));

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
        }
    }
}
