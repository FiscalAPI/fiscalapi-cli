using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FiscalApi.Cli.Updates;

public sealed partial class SemanticVersion : IComparable<SemanticVersion>
{
    private readonly int _major;
    private readonly int _minor;
    private readonly int _patch;
    private readonly string[] _prerelease;
    private readonly string _text;

    private SemanticVersion(int major, int minor, int patch, string[] prerelease, string text)
    {
        _major = major;
        _minor = minor;
        _patch = patch;
        _prerelease = prerelease;
        _text = text;
    }

    public bool IsPrerelease => _prerelease.Length > 0;

    public static bool TryParse(string? text, [NotNullWhen(true)] out SemanticVersion? version)
    {
        version = null;
        if (text is null)
        {
            return false;
        }

        var candidate = text.StartsWith('v') ? text[1..] : text;
        var match = SemVerPattern().Match(candidate);
        if (!match.Success
            || !TryParseNumber(match.Groups["major"].Value, out var major)
            || !TryParseNumber(match.Groups["minor"].Value, out var minor)
            || !TryParseNumber(match.Groups["patch"].Value, out var patch))
        {
            return false;
        }

        var prerelease = match.Groups["prerelease"].Success ? match.Groups["prerelease"].Value.Split('.') : [];
        version = new SemanticVersion(major, minor, patch, prerelease, candidate);
        return true;
    }

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var core = (_major, _minor, _patch).CompareTo((other._major, other._minor, other._patch));
        return core != 0 ? core : ComparePrerelease(_prerelease, other._prerelease);
    }

    public override string ToString() => _text;

    private static int ComparePrerelease(string[] left, string[] right)
    {
        if (left.Length == 0 || right.Length == 0)
        {
            return right.Length.CompareTo(left.Length);
        }

        for (var i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            var comparison = CompareIdentifier(left[i], right[i]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return left.Length.CompareTo(right.Length);
    }

    private static int CompareIdentifier(string left, string right)
    {
        var leftIsNumeric = IsNumeric(left);
        var rightIsNumeric = IsNumeric(right);
        if (leftIsNumeric && rightIsNumeric)
        {
            var byLength = left.Length.CompareTo(right.Length);
            return byLength != 0 ? byLength : string.CompareOrdinal(left, right);
        }

        if (leftIsNumeric != rightIsNumeric)
        {
            return leftIsNumeric ? -1 : 1;
        }

        return string.CompareOrdinal(left, right);
    }

    private static bool IsNumeric(string identifier) => identifier.All(char.IsAsciiDigit);

    private static bool TryParseNumber(string text, out int value) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);

    [GeneratedRegex(
        @"^(?<major>0|[1-9][0-9]*)\.(?<minor>0|[1-9][0-9]*)\.(?<patch>0|[1-9][0-9]*)" +
        @"(?:-(?<prerelease>(?:0|[1-9][0-9]*|[0-9]*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9]*[a-zA-Z-][0-9a-zA-Z-]*))*))?" +
        @"(?:\+[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*)?\z",
        RegexOptions.CultureInvariant)]
    private static partial Regex SemVerPattern();
}
