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
        TimeSpan? matchTimeout = null,
        int? maxDetailedMatches = null)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(input);

        cancellationToken.ThrowIfCancellationRequested();

        long startTimestamp = Stopwatch.GetTimestamp();

        string replaceWith = replacement ?? string.Empty;
        var regex = new Regex(pattern, options, matchTimeout ?? DefaultTimeout);

        // Precompute group display names once per regex instead of per match x group.
        int groupCount = regex.GetGroupNumbers().Length;
        string[] formattedGroupNames = new string[groupCount];
        for (int g = 0; g < groupCount; g++)
        {
            string rawGroupName = regex.GroupNameFromNumber(g);
            formattedGroupNames[g] = rawGroupName != g.ToString() ? $"<{rawGroupName}>" : string.Empty;
        }

        // Enumerate lazily so cancellation can abort between matches without forcing eager Count evaluation.
        MatchCollection matches = regex.Matches(input);
        var matchSnapshots = new List<MatchSnapshot>();
        int matchIndex = 0;

        foreach (Match match in matches)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (maxDetailedMatches.HasValue && matchIndex >= maxDetailedMatches.Value)
            {
                matchSnapshots.Add(new MatchSnapshot(match.Index, match.Length, string.Empty, [], []));
                matchIndex++;
                continue;
            }

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
                string formattedName = j < formattedGroupNames.Length
                    ? formattedGroupNames[j]
                    : FormatGroupName(regex, j);
                groups.Add(new GroupSnapshot(j, formattedName, group.Success, group.Value, group.Index, group.Length));
            }

            matchSnapshots.Add(new MatchSnapshot(match.Index, match.Length, match.Value, captures, groups));
            matchIndex++;
        }

        cancellationToken.ThrowIfCancellationRequested();
        string replacementResults = matchSnapshots.Count > 0
            ? regex.Replace(input, replaceWith)
            : string.Empty;

        cancellationToken.ThrowIfCancellationRequested();
        string[] splitResults = regex.Split(input);
        TimeSpan elapsed = Stopwatch.GetElapsedTime(startTimestamp);

        return new RegexEvaluationResult(matchSnapshots, replacementResults, splitResults, elapsed);
    }

    /// <summary>
    /// Returns the bitmask of <see cref="RegexOptions"/> flags that conflict with the currently selected options.
    /// Already-selected flags are excluded so users can still uncheck pre-existing conflicting options.
    /// </summary>
    public static RegexOptions GetConflictingOptions(RegexOptions selectedOptions)
    {
        RegexOptions conflicting = RegexOptions.None;

        if (selectedOptions.HasFlag(RegexOptions.NonBacktracking))
        {
            conflicting |= RegexOptions.RightToLeft | RegexOptions.ECMAScript;
        }

        if (selectedOptions.HasFlag(RegexOptions.ECMAScript))
        {
            conflicting |= RegexOptions.ExplicitCapture
                | RegexOptions.IgnorePatternWhitespace
                | RegexOptions.RightToLeft
                | RegexOptions.Singleline
                | RegexOptions.NonBacktracking;
        }

        if ((selectedOptions & (RegexOptions.RightToLeft | RegexOptions.ECMAScript)) != 0)
        {
            conflicting |= RegexOptions.NonBacktracking;
        }

        if ((selectedOptions & (RegexOptions.ExplicitCapture
            | RegexOptions.IgnorePatternWhitespace
            | RegexOptions.RightToLeft
            | RegexOptions.Singleline
            | RegexOptions.NonBacktracking)) != 0)
        {
            conflicting |= RegexOptions.ECMAScript;
        }

        return conflicting & ~selectedOptions;
    }

    private static string FormatGroupName(Regex regex, int groupNumber)
    {
        string rawGroupName = regex.GroupNameFromNumber(groupNumber);
        return rawGroupName != groupNumber.ToString() ? $"<{rawGroupName}>" : string.Empty;
    }
}
