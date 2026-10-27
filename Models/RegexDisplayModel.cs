namespace CSharpRegexTester.Models;

/// <summary>
/// Pre-formatted presentation model containing all data required to render regex results,
/// generated entirely on background threads to keep the UI thread responsive.
/// </summary>
public sealed record RegexDisplayModel(
    IReadOnlyList<MatchTreeNode> TreeNodes,
    IReadOnlyList<string> FormattedSplitResults,
    IReadOnlyList<TextSpan> HighlightSpans,
    string ReplacementResults,
    string MatchSummary,
    string StatusMessage,
    string CSharpSnippet,
    RegexEvaluationResult? EvaluationResult = null);
