namespace CSharpRegexTester.Models;

public sealed record CaptureSnapshot(string Value, int Index, int Length)
{
    public TextSpan Span => new(Index, Length);
}

public sealed record GroupSnapshot(
    int Number,
    string Name,
    bool Success,
    string Value,
    int Index,
    int Length)
{
    public TextSpan? Span => Success ? new TextSpan(Index, Length) : null;
}

public sealed record MatchSnapshot(
    int Index,
    int Length,
    string Value,
    IReadOnlyList<CaptureSnapshot> Captures,
    IReadOnlyList<GroupSnapshot> Groups)
{
    public TextSpan Span => new(Index, Length);
}

public sealed record RegexEvaluationResult(
    IReadOnlyList<MatchSnapshot> Matches,
    string ReplacementResults,
    IReadOnlyList<string> SplitResults,
    TimeSpan Elapsed);
