using System.CommandLine;

namespace FiscalApi.Cli.Tests;

public sealed class GlobalOptionsTests
{
    [Fact]
    public void Without_options_the_environment_is_test()
    {
        var settings = Parse();

        Assert.Equal(new Uri("https://test.fiscalapi.com"), settings.Environment.BaseAddress);
    }

    [Fact]
    public void Live_selects_the_live_environment()
    {
        var settings = Parse("--live");

        Assert.Equal(new Uri("https://live.fiscalapi.com"), settings.Environment.BaseAddress);
    }

    [Fact]
    public void Api_base_wins_over_live()
    {
        var settings = Parse("--live", "--api-base", "https://localhost:5001");

        Assert.Equal(new Uri("https://localhost:5001"), settings.Environment.BaseAddress);
    }

    [Fact]
    public void Api_base_wins_over_the_default()
    {
        var settings = Parse("--api-base", "http://localhost:5000/");

        Assert.Equal(new Uri("http://localhost:5000/"), settings.Environment.BaseAddress);
    }

    [Theory]
    [InlineData("https://dev.example.test/fiscalapi")]
    [InlineData("https://dev.example.test/fiscalapi/")]
    public void Api_base_keeps_its_path_prefix_when_resolving_api_routes(string apiBase)
    {
        var settings = Parse("--api-base", apiBase);

        Assert.Equal(new Uri("https://dev.example.test/fiscalapi/"), settings.Environment.BaseAddress);
        Assert.Equal(
            new Uri("https://dev.example.test/fiscalapi/api/v4/me/features"),
            new Uri(settings.Environment.BaseAddress, "api/v4/me/features"));
    }

    [Theory]
    [InlineData("localhost:5001")]
    [InlineData("ftp://localhost")]
    [InlineData("/api/v4")]
    public void Api_base_must_be_an_absolute_http_url(string apiBase)
    {
        var parseResult = ParseWith(new GlobalOptions(), "--api-base", apiBase);

        Assert.Contains(parseResult.Errors, error => error.Message.Contains("--api-base"));
    }

    [Theory]
    [InlineData("http://localhost:5000")]
    [InlineData("http://127.0.0.1:5000")]
    [InlineData("http://[::1]:5000")]
    public void Api_base_with_http_is_allowed_for_loopback(string apiBase)
    {
        var settings = Parse("--api-base", apiBase);

        Assert.Equal(Uri.UriSchemeHttp, settings.Environment.BaseAddress.Scheme);
    }

    [Theory]
    [InlineData("http://test.fiscalapi.com")]
    [InlineData("http://192.168.1.20:5000")]
    [InlineData("http://localhost.example.test:5000")]
    public void Api_base_with_http_to_another_host_is_a_usage_error(string apiBase)
    {
        var parseResult = ParseWith(new GlobalOptions(), "--api-base", apiBase);

        var error = Assert.Single(parseResult.Errors);
        Assert.Contains("--api-base", error.Message);
        Assert.Contains("https", error.Message);
        Assert.Contains("localhost", error.Message);
    }

    [Fact]
    public void Json_and_verbose_are_off_by_default()
    {
        var settings = Parse();

        Assert.False(settings.Json);
        Assert.False(settings.Verbose);
    }

    [Fact]
    public void Json_and_verbose_are_read_from_the_command_line()
    {
        var settings = Parse("--json", "--verbose");

        Assert.True(settings.Json);
        Assert.True(settings.Verbose);
    }

    [Fact]
    public void Global_options_reach_subcommands()
    {
        var globalOptions = new GlobalOptions();
        var root = new RootCommand();
        globalOptions.AddTo(root);
        root.Subcommands.Add(new Command("probe"));

        var settings = globalOptions.Read(root.Parse(["probe", "--live"]));

        Assert.Equal(ApiEnvironment.LiveBaseAddress, settings.Environment.BaseAddress);
    }

    private static GlobalSettings Parse(params string[] args)
    {
        var globalOptions = new GlobalOptions();
        var parseResult = ParseWith(globalOptions, args);
        Assert.Empty(parseResult.Errors);
        return globalOptions.Read(parseResult);
    }

    private static ParseResult ParseWith(GlobalOptions globalOptions, params string[] args)
    {
        var root = new RootCommand();
        globalOptions.AddTo(root);
        return root.Parse(args);
    }
}
