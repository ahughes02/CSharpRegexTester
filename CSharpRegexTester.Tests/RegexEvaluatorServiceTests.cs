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
            null,
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

    [Fact]
    public void Evaluate_WithCatastrophicBacktrackingAndShortTimeout_ThrowsRegexMatchTimeoutException()
    {
        string backtrackingPattern = @"(a+)+b";
        string evilInput = new string('a', 40) + "c";

        Assert.Throws<RegexMatchTimeoutException>(() =>
            RegexEvaluatorService.Evaluate(
                backtrackingPattern,
                evilInput,
                string.Empty,
                RegexOptions.None,
                CancellationToken.None,
                TimeSpan.FromMilliseconds(1)));
    }

    [Fact]
    public void Evaluate_WithMaxDetailedMatches_PreservesAllSpansWhileSkippingExcessDetailSnapshots()
    {
        RegexEvaluationResult result = RegexEvaluatorService.Evaluate(
            @"(?<ch>\w)",
            "abcde",
            null,
            RegexOptions.None,
            maxDetailedMatches: 2);

        Assert.Equal(5, result.Matches.Count);
        Assert.Equal("a", result.Matches[0].Value);
        Assert.NotEmpty(result.Matches[0].Captures);
        Assert.NotEmpty(result.Matches[0].Groups);

        Assert.Equal("b", result.Matches[1].Value);
        Assert.NotEmpty(result.Matches[1].Groups);

        // Matches past maxDetailedMatches retain spans for highlighting while omitting unused detail allocations
        Assert.Equal(new TextSpan(2, 1), result.Matches[2].Span);
        Assert.Equal(string.Empty, result.Matches[2].Value);
        Assert.Empty(result.Matches[2].Captures);
        Assert.Empty(result.Matches[2].Groups);
    }

    [Fact]
    public void GetConflictingOptions_IdentifiesIncompatibleFlagsWithoutDisablingAlreadySelectedFlags()
    {
        RegexOptions nonBacktrackingConflicts = RegexEvaluatorService.GetConflictingOptions(RegexOptions.NonBacktracking);
        Assert.True(nonBacktrackingConflicts.HasFlag(RegexOptions.RightToLeft));
        Assert.True(nonBacktrackingConflicts.HasFlag(RegexOptions.ECMAScript));
        Assert.False(nonBacktrackingConflicts.HasFlag(RegexOptions.NonBacktracking));

        RegexOptions ecmascriptConflicts = RegexEvaluatorService.GetConflictingOptions(RegexOptions.ECMAScript);
        Assert.True(ecmascriptConflicts.HasFlag(RegexOptions.ExplicitCapture));
        Assert.True(ecmascriptConflicts.HasFlag(RegexOptions.IgnorePatternWhitespace));
        Assert.True(ecmascriptConflicts.HasFlag(RegexOptions.RightToLeft));
        Assert.True(ecmascriptConflicts.HasFlag(RegexOptions.Singleline));
        Assert.True(ecmascriptConflicts.HasFlag(RegexOptions.NonBacktracking));
        Assert.False(ecmascriptConflicts.HasFlag(RegexOptions.IgnoreCase));
        Assert.False(ecmascriptConflicts.HasFlag(RegexOptions.Multiline));

        // Pre-existing conflicting selection does not lock itself out so the user can still uncheck it
        RegexOptions dualSelected = RegexEvaluatorService.GetConflictingOptions(
            RegexOptions.NonBacktracking | RegexOptions.RightToLeft);
        Assert.False(dualSelected.HasFlag(RegexOptions.NonBacktracking));
        Assert.False(dualSelected.HasFlag(RegexOptions.RightToLeft));
    }
}
