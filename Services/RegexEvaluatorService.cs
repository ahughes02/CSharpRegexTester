using System.Diagnostics;
using System.Text.RegularExpressions;
using CSharpRegexTester.Models;

namespace CSharpRegexTester.Services;

/// <summary>
/// Evaluates regular expressions against test input with timeout protection and execution timing.
/// </summary>
public static class RegexEvaluatorService
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

    public static RegexEvaluationResult Evaluate(
        string pattern,
        string input,
        string? replacement,
        RegexOptions options,
        CancellationToken cancellationToken = default,
        TimeSpan? matchTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(input);

        cancellationToken.ThrowIfCancellationRequested();

        long startTimestamp = Stopwatch.GetTimestamp();

        string replaceWith = replacement ?? string.Empty;
        var regex = new Regex(pattern, options, matchTimeout ?? DefaultTimeout);
        MatchCollection matches = regex.Matches(input);

        var matchSnapshots = new List<MatchSnapshot>(matches.Count);

        for (int i = 0; i < matches.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Match match = matches[i];

            var captures = new List<CaptureSnapshot>(match.Captures.Count);
            for (int j = 0; j < match.Captures.Count; j++)
            {
                Capture capture = match.Captures[j];
                captures.Add(new CaptureSnapshot(capture.Value, capture.Index, capture.Length));
            }

            var groups = new List<GroupSnapshot>(match.Groups.Count);
            for (int j = 0; j < match.Groups.Count; j++)
            {
                Group group = match.Groups[j];
                string rawGroupName = regex.GroupNameFromNumber(j);
                string formattedName = rawGroupName != j.ToString() ? $"<{rawGroupName}>" : string.Empty;
                groups.Add(new GroupSnapshot(j, formattedName, group.Success, group.Value, group.Index, group.Length));
            }

            matchSnapshots.Add(new MatchSnapshot(match.Index, match.Length, match.Value, captures, groups));
        }

        string replacementResults = matchSnapshots.Count > 0
            ? regex.Replace(input, replaceWith)
            : string.Empty;

        string[] splitResults = regex.Split(input);
        TimeSpan elapsed = Stopwatch.GetElapsedTime(startTimestamp);

        return new RegexEvaluationResult(matchSnapshots, replacementResults, splitResults, elapsed);
    }
}
