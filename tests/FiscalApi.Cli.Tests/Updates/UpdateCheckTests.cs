using System.Diagnostics;
using System.Net;
using System.Text.Json;
using FiscalApi.Cli.Tests.Http;
using FiscalApi.Cli.Updates;
using Microsoft.Extensions.Time.Testing;

namespace FiscalApi.Cli.Tests.Updates;

public sealed class UpdateCheckTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan RealTimeLimit = TimeSpan.FromSeconds(10);

    private static readonly string TestsDirectory = Path.Combine(Path.GetTempPath(), "fiscalapi-cli-tests");

    private readonly string _directory = Path.Combine(TestsDirectory, Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _clock = new(Now);
    private readonly StubHttpMessageHandler _github = new();
    private readonly Dictionary<string, string> _environment = [];

    private string StatePath => Path.Combine(_directory, "update-check.json");

    public void Dispose()
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (File.Exists(_directory))
                {
                    File.Delete(_directory);
                }
                else if (Directory.Exists(_directory))
                {
                    Directory.Delete(_directory, recursive: true);
                }

                return;
            }
            catch (IOException) when (attempt < 20)
            {
                Thread.Sleep(50);
            }
        }
    }

    [Fact]
    public async Task Newer_release_gives_a_notice_with_the_download_url()
    {
        _github.RespondJson(HttpStatusCode.OK, Release("v0.2.0"));

        var notice = await StartAsync();

        Assert.NotNull(notice);
        Assert.Equal("0.1.0", notice.Current.ToString());
        Assert.Equal("0.2.0", notice.Latest.ToString());
        Assert.Equal("https://github.com/FiscalAPI/fiscalapi-cli/releases/tag/v0.2.0", notice.DownloadUrl.ToString());
    }

    [Fact]
    public async Task Asks_github_anonymously_for_the_latest_release_of_the_cli()
    {
        _github.RespondJson(HttpStatusCode.OK, Release("v0.2.0"));

        await StartAsync();

        var request = Assert.Single(_github.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://api.github.com/repos/FiscalAPI/fiscalapi-cli/releases/latest", request.RequestUri?.ToString());
        Assert.Equal("fiscalapi-cli/0.1.0", request.Headers.UserAgent.ToString());
        Assert.Contains("application/vnd.github+json", request.Headers.Accept.ToString());
        Assert.False(request.Headers.Contains("X-API-KEY"));
        Assert.False(request.Headers.Contains("X-TENANT-KEY"));
        Assert.Null(request.Headers.Authorization);
    }

    [Fact]
    public async Task Notice_names_both_versions_and_the_download_url()
    {
        _github.RespondJson(HttpStatusCode.OK, Release("v0.2.0"));
        var notice = await StartAsync();
        var error = new StringWriter();
        Assert.NotNull(notice);

        await notice.WriteToAsync(error);

        var text = error.ToString();
        Assert.Contains("fiscalapi 0.2.0 is available (you have 0.1.0).", text);
        Assert.Contains("Download it from https://github.com/FiscalAPI/fiscalapi-cli/releases/tag/v0.2.0", text);
    }

    [Fact]
    public async Task Asks_at_most_once_every_24_hours()
    {
        _github.RespondJson(HttpStatusCode.OK, Release("v0.2.0"), times: 2);
        await StartAsync();

        _clock.Advance(TimeSpan.FromHours(24) - TimeSpan.FromSeconds(1));
        var withinTheDay = await StartAsync();

        Assert.Null(withinTheDay);
        Assert.Single(_github.Requests);

        _clock.Advance(TimeSpan.FromSeconds(1));
        var nextDay = await StartAsync();

        Assert.NotNull(nextDay);
        Assert.Equal(2, _github.Requests.Count);
    }

    [Fact]
    public async Task The_daily_check_survives_between_processes_in_the_state_file()
    {
        _github.RespondJson(HttpStatusCode.OK, Release("v0.2.0"));
        await StartAsync();

        var anotherProcess = await StartAsync(CreateCheck());

        Assert.Null(anotherProcess);
        Assert.Single(_github.Requests);
        using var state = JsonDocument.Parse(await File.ReadAllTextAsync(StatePath));
        Assert.Equal(Now, state.RootElement.GetProperty("last_checked_at").GetDateTimeOffset());
    }

    [Fact]
    public async Task Json_output_turns_the_check_off()
    {
        var notice = await StartAsync(json: true);

        Assert.Null(notice);
        Assert.Empty(_github.Requests);
        Assert.False(File.Exists(StatePath));
    }

    [Theory]
    [InlineData("CI", "true")]
    [InlineData("CI", "1")]
    [InlineData("CI", "TRUE")]
    [InlineData("FISCALAPI_NO_UPDATE_CHECK", "1")]
    [InlineData("FISCALAPI_NO_UPDATE_CHECK", "true")]
    public async Task Ci_or_the_opt_out_variable_turns_the_check_off(string variable, string value)
    {
        _environment[variable] = value;

        var notice = await StartAsync();

        Assert.Null(notice);
        Assert.Empty(_github.Requests);
        Assert.False(File.Exists(StatePath));
    }

    [Theory]
    [InlineData("CI", "false")]
    [InlineData("CI", "0")]
    [InlineData("CI", "")]
    [InlineData("FISCALAPI_NO_UPDATE_CHECK", "0")]
    [InlineData("FISCALAPI_NO_UPDATE_CHECK", "false")]
    public async Task False_values_of_the_variables_keep_the_check_on(string variable, string value)
    {
        _environment[variable] = value;
        _github.RespondJson(HttpStatusCode.OK, Release("v0.2.0"));

        var notice = await StartAsync();

        Assert.NotNull(notice);
    }

    [Fact]
    public async Task Redirected_stderr_turns_the_check_off()
    {
        var notice = await StartAsync(CreateCheck(errorRedirected: true));

        Assert.Null(notice);
        Assert.Empty(_github.Requests);
    }

    [Fact]
    public async Task Network_failure_gives_no_notice_and_still_counts_as_the_daily_check()
    {
        _github.FailWithNetworkError();

        var notice = await StartAsync();
        var again = await StartAsync();

        Assert.Null(notice);
        Assert.Null(again);
        Assert.Single(_github.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, """{"message":"Not Found","status":"404"}""")]
    [InlineData(HttpStatusCode.Forbidden, """{"message":"API rate limit exceeded"}""")]
    [InlineData(HttpStatusCode.InternalServerError, "<html>error</html>")]
    [InlineData(HttpStatusCode.MovedPermanently, "")]
    [InlineData(HttpStatusCode.OK, "no es json")]
    [InlineData(HttpStatusCode.OK, "[]")]
    [InlineData(HttpStatusCode.OK, "{}")]
    [InlineData(HttpStatusCode.OK, """{"tag_name":null}""")]
    [InlineData(HttpStatusCode.OK, """{"tag_name":42}""")]
    [InlineData(HttpStatusCode.OK, """{"tag_name":"latest"}""")]
    [InlineData(HttpStatusCode.OK, """{"tag_name":"v0.2"}""")]
    [InlineData(HttpStatusCode.OK, """{"tag_name":"v0.2.0\u001b[31m"}""")]
    [InlineData(HttpStatusCode.OK, """{"tag_name":"v0.2.0","draft":true,"prerelease":false}""")]
    [InlineData(HttpStatusCode.OK, """{"tag_name":"v0.2.0","draft":false,"prerelease":true}""")]
    [InlineData(HttpStatusCode.OK, """{"tag_name":"v0.2.0-rc.1","draft":false,"prerelease":false}""")]
    public async Task Unusable_responses_give_no_notice_without_failing(HttpStatusCode status, string body)
    {
        _github.Respond(() => new HttpResponseMessage(status) { Content = new StringContent(body) });

        var notice = await StartAsync();

        Assert.Null(notice);
        Assert.Single(_github.Requests);
    }

    [Fact]
    public async Task Oversized_response_gives_no_notice()
    {
        var notes = new string('x', 2 * 1024 * 1024);
        _github.RespondJson(HttpStatusCode.OK, $$"""{"tag_name":"v0.2.0","draft":false,"prerelease":false,"body":"{{notes}}"}""");

        var notice = await StartAsync();

        Assert.Null(notice);
    }

    [Fact]
    public async Task Api_that_never_answers_does_not_block_and_gives_up_at_2_seconds()
    {
        _github.NeverRespond();

        var check = CreateCheck().Start(Settings(), CancellationToken.None);

        Assert.False(check.IsCompleted);
        _clock.Advance(UpdateCheck.Timeout - TimeSpan.FromMilliseconds(1));
        Assert.False(check.IsCompleted);

        _clock.Advance(TimeSpan.FromMilliseconds(1));

        Assert.Null(await check.WaitAsync(RealTimeLimit));
    }

    [Fact]
    public async Task With_the_real_clock_a_silent_api_costs_at_most_the_2_second_budget()
    {
        _github.NeverRespond();
        var check = new UpdateCheck(_github, new UpdateCheckStateFile(StatePath), Environment, false, TimeProvider.System, "0.1.0");
        var elapsed = Stopwatch.StartNew();

        var notice = await check.Start(Settings(), CancellationToken.None).WaitAsync(RealTimeLimit);

        Assert.Null(notice);
        Assert.InRange(elapsed.Elapsed, UpdateCheck.Timeout - TimeSpan.FromMilliseconds(100), UpdateCheck.Timeout + TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Ctrl_c_ends_the_check_without_failing()
    {
        _github.NeverRespond();
        using var cancellation = new CancellationTokenSource();

        var check = CreateCheck().Start(Settings(), cancellation.Token);
        await cancellation.CancelAsync();

        Assert.Null(await check.WaitAsync(RealTimeLimit));
    }

    [Fact]
    public async Task Corrupt_state_file_counts_as_never_checked_and_is_rewritten()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(StatePath, "{ esto no es json");
        _github.RespondJson(HttpStatusCode.OK, Release("v0.2.0"));

        var notice = await StartAsync();
        var again = await StartAsync();

        Assert.NotNull(notice);
        Assert.Null(again);
        Assert.Single(_github.Requests);
    }

    [Fact]
    public async Task Check_time_in_the_future_counts_as_due_after_the_clock_goes_back()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(StatePath, $$"""{"last_checked_at":"{{Now.AddDays(3):O}}"}""");
        _github.RespondJson(HttpStatusCode.OK, Release("v0.2.0"));

        var notice = await StartAsync();

        Assert.NotNull(notice);
    }

    [Fact]
    public async Task State_location_that_cannot_be_written_skips_the_check_without_failing()
    {
        Directory.CreateDirectory(TestsDirectory);
        await File.WriteAllTextAsync(_directory, "no soy una carpeta");

        var notice = await StartAsync();

        Assert.Null(notice);
        Assert.Empty(_github.Requests);
    }

    [Fact]
    public async Task State_file_that_cannot_be_read_skips_the_check_without_failing()
    {
        Directory.CreateDirectory(StatePath);

        var notice = await StartAsync();

        Assert.Null(notice);
        Assert.Empty(_github.Requests);
    }

    [Theory]
    [InlineData("0.1.0", "v0.2.0", true)]
    [InlineData("0.1.0", "0.2.0", true)]
    [InlineData("0.9.0", "v0.10.0", true)]
    [InlineData("0.1.9", "v0.1.10", true)]
    [InlineData("1.0.0-rc.1", "v1.0.0", true)]
    [InlineData("0.1.0", "v0.1.0", false)]
    [InlineData("0.2.0", "v0.1.9", false)]
    [InlineData("1.0.0", "v1.0.0+build.5", false)]
    [InlineData("1.1.0-rc.1", "v1.0.0", false)]
    public async Task Versions_are_compared_semantically(string current, string latestTag, bool expectNotice)
    {
        _github.RespondJson(HttpStatusCode.OK, Release(latestTag));

        var notice = await StartAsync(CreateCheck(currentVersion: current));

        Assert.Equal(expectNotice, notice is not null);
    }

    [Fact]
    public async Task Current_version_that_is_not_semantic_skips_the_check()
    {
        var notice = await StartAsync(CreateCheck(currentVersion: "dev"));

        Assert.Null(notice);
        Assert.Empty(_github.Requests);
    }

    [Fact]
    public void Linux_state_directory_follows_xdg_state_home_or_falls_back_to_local_state()
    {
        Assert.Equal(
            Path.Combine("/xdg/state", "fiscalapi-cli"),
            UpdateCheckStateFile.LinuxStateDirectory(name => name == "XDG_STATE_HOME" ? "/xdg/state" : "/home/ana"));
        Assert.Equal(
            Path.Combine("/home/ana", ".local", "state", "fiscalapi-cli"),
            UpdateCheckStateFile.LinuxStateDirectory(name => name == "HOME" ? "/home/ana" : null));
        Assert.Equal(
            Path.Combine("/home/ana", ".local", "state", "fiscalapi-cli"),
            UpdateCheckStateFile.LinuxStateDirectory(name => name == "XDG_STATE_HOME" ? "relativa" : "/home/ana"));
        Assert.Null(UpdateCheckStateFile.LinuxStateDirectory(_ => null));
    }

    [Fact]
    public void State_file_for_the_current_user_lives_in_a_folder_of_the_cli()
    {
        var stateFile = UpdateCheckStateFile.ForCurrentUser(System.Environment.GetEnvironmentVariable);

        Assert.NotNull(stateFile);
        Assert.Equal("update-check.json", Path.GetFileName(stateFile.Path));
        Assert.Equal("fiscalapi-cli", Path.GetFileName(Path.GetDirectoryName(stateFile.Path)));
    }

    [Theory]
    [InlineData("update-check.json")]
    [InlineData("")]
    public void State_file_without_a_parent_directory_is_rejected(string path)
    {
        Assert.Throws<ArgumentException>("path", () => new UpdateCheckStateFile(path));
    }

    [Fact]
    public void State_file_at_the_file_system_root_is_rejected()
    {
        var root = Path.DirectorySeparatorChar.ToString();

        Assert.Throws<ArgumentException>("path", () => new UpdateCheckStateFile(root));
    }

    private string? Environment(string name) => _environment.GetValueOrDefault(name);

    private UpdateCheck CreateCheck(string currentVersion = "0.1.0", bool errorRedirected = false) =>
        new(_github, new UpdateCheckStateFile(StatePath), Environment, errorRedirected, _clock, currentVersion);

    private Task<UpdateNotice?> StartAsync(bool json = false) => StartAsync(CreateCheck(), json);

    private static Task<UpdateNotice?> StartAsync(UpdateCheck check, bool json = false) =>
        check.Start(Settings(json), CancellationToken.None).WaitAsync(RealTimeLimit);

    private static GlobalSettings Settings(bool json = false) => new(ApiEnvironment.Resolve(null, live: false), json, Verbose: false);

    private static string Release(string tag) =>
        $$"""{"tag_name":"{{tag}}","html_url":"https://github.com/FiscalAPI/fiscalapi-cli/releases/tag/{{tag}}","draft":false,"prerelease":false}""";
}
