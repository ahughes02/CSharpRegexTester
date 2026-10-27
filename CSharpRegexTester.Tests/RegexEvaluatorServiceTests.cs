using System.Text.RegularExpressions;
using CSharpRegexTester.Models;
using CSharpRegexTester.Services;

namespace CSharpRegexTester.Tests;

public class RegexEvaluatorServiceTests
{
    [Fact]
    public void Evaluate_WithMatchingNamedAndNumberedGroups_ReturnsFullSnapshotsAndReplacement()
    {
        string pattern = @"(?<year>\d{4})-(?<month>\d{2})(?:-(\d{2}))?";
        string input = "Dates: 2026-10-27 and 2025-05";
        string replacement = "${month}/${year}";

        RegexEvaluationResult result = RegexEvaluatorService.Evaluate(
            pattern,
            input,
            replacement,
            RegexOptions.IgnoreCase);

        Assert.Equal(2, result.Matches.Count);

        MatchSnapshot first = result.Matches[0];
        Assert.Equal("2026-10-27", first.Value);
        Assert.Equal(7, first.Index);
        Assert.Equal(10, first.Length);
        Assert.Single(first.Captures);
        Assert.Equal(4, first.Groups.Count);
        Assert.Equal(string.Empty, first.Groups[0].Name);
        Assert.Equal(string.Empty, first.Groups[1].Name);
        Assert.Equal("27", first.Groups[1].Value);
        Assert.Equal("<year>", first.Groups[2].Name);
        Assert.Equal("2026", first.Groups[2].Value);
        Assert.Equal("<month>", first.Groups[3].Name);
        Assert.Equal("10", first.Groups[3].Value);

        MatchSnapshot second = result.Matches[1];
        Assert.Equal("2025-05", second.Value);
        Assert.False(second.Groups[1].Success);
        Assert.Null(second.Groups[1].Span);

        Assert.Equal("Dates: 10/2026 and 05/2025", result.ReplacementResults);
        Assert.True(result.Elapsed >= TimeSpan.Zero);
    }

    [Fact]
    public void Evaluate_WhenNoMatches_ReturnsEmptyMatchesAndEmptyReplacement()
    {
        RegexEvaluationResult result = RegexEvaluatorService.Evaluate(
            @"\d+",
            "no digits here",
            "X",
            RegexOptions.None);

        Assert.Empty(result.Matches);
        Assert.Equal(string.Empty, result.ReplacementResults);
        Assert.Equal(["no digits here"], result.SplitResults);
    }

    [Fact]
    public void Evaluate_SplitPattern_SplitsInputCorrectly()
    {
        RegexEvaluationResult result = RegexEvaluatorService.Evaluate(
            @",\s*",
            "alpha, beta,  gamma",
            null!,
            RegexOptions.None);

        Assert.Equal(["alpha", "beta", "gamma"], result.SplitResults);
    }

    [Fact]
    public void Evaluate_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            RegexEvaluatorService.Evaluate(@"\w+", "some input", "", RegexOptions.None, cts.Token));
    }

    [Fact]
    public void Evaluate_WithInvalidRegex_ThrowsRegexParseException()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            RegexEvaluatorService.Evaluate("[unclosed", "input", "", RegexOptions.None));
    }
}
