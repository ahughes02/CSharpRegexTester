using System.Text.RegularExpressions;
using CSharpRegexTester.Models;

namespace CSharpRegexTester.Services;

/// <summary>
/// Stateless presentation service that formats regex evaluation results into display-ready models
/// and hierarchical node graphs on background threads, completely decoupling expensive formatting
/// and tree structuring from the WPF UI thread.
/// </summary>
public static class RegexPresentationService
{
    public const int MaxTreeMatches = 1000;
    public const int MaxSplitDisplayItems = 1000;

    public static RegexDisplayModel BuildModel(
        RegexEvaluationResult result,
        string pattern,
        string replacement,
        RegexOptions options,
        bool expandCaptures,
        bool expandGroups,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        cancellationToken.ThrowIfCancellationRequested();

        // 1. Format split results
        int splitCount = Math.Min(result.SplitResults.Count, MaxSplitDisplayItems);
        var formattedSplits = new List<string>(splitCount + (result.SplitResults.Count > MaxSplitDisplayItems ? 1 : 0));
        for (int i = 0; i < splitCount; i++)
        {
            if (i % 256 == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            string part = result.SplitResults[i];
            formattedSplits.Add($"[{i}] \"{part}\" ({part.Length} chars)");
        }
        if (result.SplitResults.Count > MaxSplitDisplayItems)
        {
            formattedSplits.Add($"... and {result.SplitResults.Count - MaxSplitDisplayItems:N0} more split items.");
        }

        // 2. Format C# Snippet
        string csharpSnippet = CSharpCodeGenerator.GenerateSnippet(pattern, replacement, options);

        double elapsedMs = result.Elapsed.TotalMilliseconds;

        // 3. Build tree nodes & highlights
        if (result.Matches.Count > 0)
        {
            string rootHeader = result.Matches.Count > MaxTreeMatches
                ? $"Matches ({result.Matches.Count:N0} total, showing first {MaxTreeMatches:N0})"
                : $"Matches ({result.Matches.Count:N0})";

            int treeDisplayCount = Math.Min(result.Matches.Count, MaxTreeMatches);
            var highlightSpans = new List<TextSpan>(result.Matches.Count);
            var matchNodes = new List<MatchTreeNode>(treeDisplayCount + (result.Matches.Count > MaxTreeMatches ? 1 : 0));

            for (int i = 0; i < result.Matches.Count; i++)
            {
                if (i % 256 == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                MatchSnapshot match = result.Matches[i];
                highlightSpans.Add(match.Span);

                if (i < treeDisplayCount)
                {
                    // Captures
                    var captureNodes = new List<MatchTreeNode>(match.Captures.Count);
                    for (int j = 0; j < match.Captures.Count; j++)
                    {
                        CaptureSnapshot cap = match.Captures[j];
                        captureNodes.Add(new MatchTreeNode(
                            $"[{j}] - [{cap.Value}] (Index: {cap.Index}, Length: {cap.Length})",
                            MatchTreeNodeType.Capture,
                            cap.Span));
                    }

                    var capturesContainer = new MatchTreeNode(
                        $"Captures ({match.Captures.Count})",
                        MatchTreeNodeType.CapturesContainer,
                        children: captureNodes,
                        isExpanded: expandCaptures);

                    // Groups
                    var groupNodes = new List<MatchTreeNode>(match.Groups.Count);
                    for (int j = 0; j < match.Groups.Count; j++)
                    {
                        GroupSnapshot grp = match.Groups[j];
                        string grpHeader = grp.Success
                            ? $"[{grp.Number}]{grp.Name} - [{grp.Value}] (Index: {grp.Index}, Length: {grp.Length})"
                            : $"[{grp.Number}]{grp.Name} - <not matched>";

                        groupNodes.Add(new MatchTreeNode(
                            grpHeader,
                            MatchTreeNodeType.Group,
                            grp.Span));
                    }

                    var groupsContainer = new MatchTreeNode(
                        $"Groups ({match.Groups.Count})",
                        MatchTreeNodeType.GroupsContainer,
                        children: groupNodes,
                        isExpanded: expandGroups);

                    matchNodes.Add(new MatchTreeNode(
                        $"[{i}] - [{match.Value}] (Index: {match.Index}, Length: {match.Length})",
                        MatchTreeNodeType.Match,
                        match.Span,
                        children: [capturesContainer, groupsContainer],
                        isExpanded: true));
                }
            }

            if (result.Matches.Count > MaxTreeMatches)
            {
                matchNodes.Add(new MatchTreeNode(
                    $"... {result.Matches.Count - MaxTreeMatches:N0} additional matches not shown in tree for performance.",
                    MatchTreeNodeType.Info));
            }

            var rootNode = new MatchTreeNode(
                rootHeader,
                MatchTreeNodeType.Root,
                children: matchNodes,
                isExpanded: true);

            string matchLabel = result.Matches.Count == 1 ? "1 match" : $"{result.Matches.Count:N0} matches";
            string matchSummary = $"{matchLabel} ({elapsedMs:F2} ms)";
            string statusMessage = $"Found {matchLabel} and {result.SplitResults.Count:N0} split segment(s) in {elapsedMs:F2} ms. Click any match/group in the tree to locate it.";

            return new RegexDisplayModel(
                TreeNodes: [rootNode],
                FormattedSplitResults: formattedSplits,
                HighlightSpans: highlightSpans,
                ReplacementResults: result.ReplacementResults,
                MatchSummary: matchSummary,
                StatusMessage: statusMessage,
                CSharpSnippet: csharpSnippet,
                EvaluationResult: result);
        }
        else
        {
            string matchSummary = $"0 matches ({elapsedMs:F2} ms)";
            string statusMessage = $"No matches found ({elapsedMs:F2} ms).";

            return new RegexDisplayModel(
                TreeNodes: [new MatchTreeNode("No Matches", MatchTreeNodeType.Message)],
                FormattedSplitResults: formattedSplits,
                HighlightSpans: [],
                ReplacementResults: string.Empty,
                MatchSummary: matchSummary,
                StatusMessage: statusMessage,
                CSharpSnippet: csharpSnippet,
                EvaluationResult: result);
        }
    }

    public static RegexDisplayModel CreateTimeoutModel(
        RegexMatchTimeoutException ex,
        string pattern,
        string replacement,
        RegexOptions options)
    {
        var treeNode = new MatchTreeNode(
            "Regex Timeout (Catastrophic Backtracking Protection)",
            MatchTreeNodeType.Root,
            children:
            [
                new MatchTreeNode($"Timed out after {ex.MatchTimeout.TotalSeconds:F1}s.", MatchTreeNodeType.Info),
                new MatchTreeNode("Tip: Try enabling 'NonBacktracking' or simplifying nested quantifiers.", MatchTreeNodeType.Info)
            ],
            isExpanded: true);

        string snippet = CSharpCodeGenerator.GenerateSnippet(pattern, replacement, options);

        return new RegexDisplayModel(
            TreeNodes: [treeNode],
            FormattedSplitResults: [],
            HighlightSpans: [],
            ReplacementResults: string.Empty,
            MatchSummary: "Timed out",
            StatusMessage: $"Regex evaluation timed out after {ex.MatchTimeout.TotalSeconds:F1} seconds.",
            CSharpSnippet: snippet);
    }

    public static RegexDisplayModel CreateExceptionModel(
        Exception ex,
        string pattern,
        string replacement,
        RegexOptions options)
    {
        var treeNode = new MatchTreeNode(
            "Exception",
            MatchTreeNodeType.Root,
            children:
            [
                new MatchTreeNode($"Message: {ex.Message}", MatchTreeNodeType.Info),
                new MatchTreeNode($"Source: {ex.Source}", MatchTreeNodeType.Info)
            ],
            isExpanded: true);

        string snippet = CSharpCodeGenerator.GenerateSnippet(pattern, replacement, options);

        return new RegexDisplayModel(
            TreeNodes: [treeNode],
            FormattedSplitResults: [],
            HighlightSpans: [],
            ReplacementResults: string.Empty,
            MatchSummary: "Invalid Pattern",
            StatusMessage: $"Error: {ex.Message}",
            CSharpSnippet: snippet);
    }

    public static RegexDisplayModel CreateEmptyModel(string statusMessage)
    {
        return new RegexDisplayModel(
            TreeNodes: [],
            FormattedSplitResults: [],
            HighlightSpans: [],
            ReplacementResults: string.Empty,
            MatchSummary: string.Empty,
            StatusMessage: statusMessage,
            CSharpSnippet: string.Empty);
    }

    public static RegexDisplayModel CreateLoadingModel()
    {
        return new RegexDisplayModel(
            TreeNodes: [new MatchTreeNode("Working...", MatchTreeNodeType.Message)],
            FormattedSplitResults: [],
            HighlightSpans: [],
            ReplacementResults: string.Empty,
            MatchSummary: string.Empty,
            StatusMessage: "Evaluating regular expression...",
            CSharpSnippet: string.Empty);
    }

    public static void UpdateExpansion(
        IReadOnlyList<MatchTreeNode> roots,
        bool expandCaptures,
        bool expandGroups)
    {
        foreach (MatchTreeNode root in roots)
        {
            foreach (MatchTreeNode matchNode in root.Children)
            {
                if (matchNode.Children.Count >= 1 && matchNode.Children[0].NodeType == MatchTreeNodeType.CapturesContainer)
                {
                    matchNode.Children[0].IsExpanded = expandCaptures;
                }

                if (matchNode.Children.Count >= 2 && matchNode.Children[1].NodeType == MatchTreeNodeType.GroupsContainer)
                {
                    matchNode.Children[1].IsExpanded = expandGroups;
                }
            }
        }
    }
}
