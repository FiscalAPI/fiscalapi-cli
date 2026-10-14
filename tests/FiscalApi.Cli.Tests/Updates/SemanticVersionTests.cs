using FiscalApi.Cli.Updates;

namespace FiscalApi.Cli.Tests.Updates;

/// <summary>Versiones semánticas 2.0.0 (semver.org): las del CLI y las de los tags de sus releases.</summary>
public sealed class SemanticVersionTests
{
    [Theory]
    [InlineData("0.1.0", "0.1.0")]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.0.0-rc.1", "1.0.0-rc.1")]
    [InlineData("1.0.0-alpha-1.x-y", "1.0.0-alpha-1.x-y")]
    [InlineData("1.0.0+20261013.sha.4fd9a7f", "1.0.0+20261013.sha.4fd9a7f")]
    [InlineData("v10.20.30-beta.2+exp", "10.20.30-beta.2+exp")]
    public void Valid_versions_are_parsed_without_the_tag_prefix(string text, string expected)
    {
        Assert.True(SemanticVersion.TryParse(text, out var version));
        Assert.Equal(expected, version.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("v")]
    [InlineData("1.0")]
    [InlineData("1.0.0.0")]
    [InlineData("01.0.0")]
    [InlineData("1.0.0-")]
    [InlineData("1.0.0-01")]
    [InlineData("1.0.0-rc..1")]
    [InlineData("1.0.0+")]
    [InlineData(" 1.0.0")]
    [InlineData("1.0.0\n")]
    [InlineData("V1.0.0")]
    [InlineData("vv1.0.0")]
    [InlineData("１.0.0")]
    [InlineData("99999999999.0.0")]
    [InlineData("latest")]
    public void Invalid_versions_are_rejected(string? text)
    {
        Assert.False(SemanticVersion.TryParse(text, out _));
    }

    [Fact]
    public void Precedence_follows_the_semver_example_chain()
    {
        string[] ordered =
        [
            "1.0.0-alpha", "1.0.0-alpha.1", "1.0.0-alpha.beta", "1.0.0-beta", "1.0.0-beta.2", "1.0.0-beta.11",
            "1.0.0-rc.1", "1.0.0", "1.0.1", "1.1.0", "1.10.0", "2.0.0"
        ];

        for (var i = 1; i < ordered.Length; i++)
        {
            Assert.True(Parse(ordered[i - 1]).CompareTo(Parse(ordered[i])) < 0, $"{ordered[i - 1]} < {ordered[i]}");
            Assert.True(Parse(ordered[i]).CompareTo(Parse(ordered[i - 1])) > 0, $"{ordered[i]} > {ordered[i - 1]}");
        }
    }

    [Fact]
    public void Build_metadata_does_not_change_precedence()
    {
        Assert.Equal(0, Parse("1.0.0+a").CompareTo(Parse("1.0.0+b")));
        Assert.Equal(0, Parse("v1.0.0").CompareTo(Parse("1.0.0")));
    }

    [Fact]
    public void Long_numeric_prerelease_identifiers_compare_by_value_without_overflow()
    {
        Assert.True(Parse("1.0.0-99999999999999999999").CompareTo(Parse("1.0.0-100000000000000000000")) < 0);
    }

    [Fact]
    public void Prerelease_is_detected()
    {
        Assert.True(Parse("1.0.0-rc.1").IsPrerelease);
        Assert.False(Parse("1.0.0+build").IsPrerelease);
    }

    private static SemanticVersion Parse(string text)
    {
        Assert.True(SemanticVersion.TryParse(text, out var version), text);
        return version;
    }
}
