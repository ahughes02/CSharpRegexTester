using System.ComponentModel;
using System.Text.RegularExpressions;
using CSharpRegexTester.Models;
using CSharpRegexTester.Services;

namespace CSharpRegexTester.Tests;

public class RegexPresentationServiceTests
{
    [Fact]
    public void BuildModel_NullResult_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            RegexPresentationService.BuildModel(null!, "pattern", "replacement", RegexOptions.None, false, false));
    }

    [Fact]
    public void BuildModel_WithMatches_FormatsCorrectlyAndExtractsSpans()
    {
        var evalResult = RegexEvaluatorService.Evaluate(
            @"(?<word>[A-Za-z]+)|(?<num>\d+)",
            "Hello 123",
            "[$0]",
            RegexOptions.None);

        RegexDisplayModel model = RegexPresentationService.BuildModel(
            evalResult,
            @"(?<word>[A-Za-z]+)|(?<num>\d+)",
            "[$0]",
            RegexOptions.None,
            expandCaptures: true,
            expandGroups: false);

        Assert.NotNull(model);
        Assert.Equal(2, model.HighlightSpans.Count);
        Assert.Equal(new TextSpan(0, 5), model.HighlightSpans[0]);
        Assert.Equal(new TextSpan(6, 3), model.HighlightSpans[1]);

        Assert.Single(model.TreeNodes);
        MatchTreeNode rootNode = model.TreeNodes[0];
        Assert.Equal(MatchTreeNodeType.Root, rootNode.NodeType);
        Assert.Equal("Matches (2)", rootNode.Header);
        Assert.True(rootNode.IsExpanded);
        Assert.Equal(2, rootNode.Children.Count);

        // First match: "Hello"
        MatchTreeNode firstMatch = rootNode.Children[0];
        Assert.Equal(MatchTreeNodeType.Match, firstMatch.NodeType);
        Assert.Equal(new TextSpan(0, 5), firstMatch.Span);
        Assert.Equal(2, firstMatch.Children.Count);

        MatchTreeNode capturesContainer = firstMatch.Children[0];
        Assert.Equal(MatchTreeNodeType.CapturesContainer, capturesContainer.NodeType);
        Assert.True(capturesContainer.IsExpanded);
        Assert.Single(capturesContainer.Children);
        Assert.Equal(new TextSpan(0, 5), capturesContainer.Children[0].Span);

        MatchTreeNode groupsContainer = firstMatch.Children[1];
        Assert.Equal(MatchTreeNodeType.GroupsContainer, groupsContainer.NodeType);
        Assert.False(groupsContainer.IsExpanded);
        Assert.True(groupsContainer.Children.Count >= 2);

        // Second match has unmatched group for <num>
        MatchTreeNode secondMatch = rootNode.Children[1];
        MatchTreeNode secondGroups = secondMatch.Children[1];
        Assert.Contains(secondGroups.Children, g => g.Header.Contains("<not matched>"));

        Assert.Contains("2 matches", model.MatchSummary);
        Assert.Contains("2 matches", model.StatusMessage);
        Assert.Contains("[GeneratedRegex", model.CSharpSnippet);
        Assert.Equal("[Hello] [123]", model.ReplacementResults);
    }

    [Fact]
    public void BuildModel_SingleMatch_FormatsSingularGrammar()
    {
        var evalResult = RegexEvaluatorService.Evaluate(
            @"Hello",
            "Hello world",
            "",
            RegexOptions.None);

        RegexDisplayModel model = RegexPresentationService.BuildModel(
            evalResult,
            @"Hello",
            "",
            RegexOptions.None,
            expandCaptures: false,
            expandGroups: false);

        Assert.Contains("1 match", model.MatchSummary);
        Assert.Contains("Found 1 match", model.StatusMessage);
        Assert.Equal("Matches (1)", model.TreeNodes[0].Header);
    }

    [Fact]
    public void BuildModel_WithZeroMatches_FormatsEmptyState()
    {
        var evalResult = RegexEvaluatorService.Evaluate(
            @"xyz",
            "Hello world",
            "",
            RegexOptions.None);

        RegexDisplayModel model = RegexPresentationService.BuildModel(
            evalResult,
            @"xyz",
            "",
            RegexOptions.None,
            expandCaptures: false,
            expandGroups: false);

        Assert.Empty(model.HighlightSpans);
        Assert.Equal(string.Empty, model.ReplacementResults);
        Assert.Contains("0 matches", model.MatchSummary);
        Assert.Contains("No matches found", model.StatusMessage);
        Assert.Single(model.TreeNodes);
        Assert.Equal("No Matches", model.TreeNodes[0].Header);
        Assert.Equal(MatchTreeNodeType.Message, model.TreeNodes[0].NodeType);
    }

    [Fact]
    public void BuildModel_ExceedingMaxLimits_TruncatesTreeAndSplits()
    {
        var matches = new List<MatchSnapshot>();
        for (int i = 0; i < 1005; i++)
        {
            matches.Add(new MatchSnapshot(i, 1, "a", [], []));
        }

        var splits = new List<string>();
        for (int s = 0; s < 1005; s++)
        {
            splits.Add("split");
        }

        var largeResult = new RegexEvaluationResult(
            matches,
            "replaced",
            splits,
            TimeSpan.FromMilliseconds(42));

        RegexDisplayModel model = RegexPresentationService.BuildModel(
            largeResult,
            "a",
            "b",
            RegexOptions.None,
            expandCaptures: false,
            expandGroups: false);

        // Splits capping
        Assert.Equal(RegexPresentationService.MaxSplitDisplayItems + 1, model.FormattedSplitResults.Count);
        Assert.Contains("more split items", model.FormattedSplitResults[^1]);

        // Tree capping
        MatchTreeNode root = model.TreeNodes[0];
        Assert.Contains("showing first", root.Header);
        Assert.Equal(RegexPresentationService.MaxTreeMatches + 1, root.Children.Count);
        Assert.Contains("additional matches not shown", root.Children[^1].Header);
        Assert.Equal(MatchTreeNodeType.Info, root.Children[^1].NodeType);
    }

    [Fact]
    public void BuildModel_HonorsCancellationToken()
    {
        var evalResult = RegexEvaluatorService.Evaluate(@"\w+", "abc def", "", RegexOptions.None);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            RegexPresentationService.BuildModel(
                evalResult,
                @"\w+",
                "",
                RegexOptions.None,
                false,
                false,
                cts.Token));
    }

    [Fact]
    public void CreateTimeoutModel_SetsExpectedHeadersAndStatus()
    {
        var timeoutEx = new RegexMatchTimeoutException("input", "pattern", TimeSpan.FromSeconds(2));
        RegexDisplayModel model = RegexPresentationService.CreateTimeoutModel(
            timeoutEx,
            "pattern",
            "replacement",
            RegexOptions.None);

        Assert.Equal("Timed out", model.MatchSummary);
        Assert.Contains("timed out after 2.0 seconds", model.StatusMessage);
        Assert.Single(model.TreeNodes);
        Assert.Contains("Regex Timeout", model.TreeNodes[0].Header);
        Assert.Equal(2, model.TreeNodes[0].Children.Count);
        Assert.Contains("[GeneratedRegex", model.CSharpSnippet);
    }

    [Fact]
    public void CreateExceptionModel_SetsExpectedHeadersAndStatus()
    {
        var ex = new ArgumentException("Invalid pattern quantifier");
        RegexDisplayModel model = RegexPresentationService.CreateExceptionModel(
            ex,
            "[invalid",
            "",
            RegexOptions.None);

        Assert.Equal("Invalid Pattern", model.MatchSummary);
        Assert.Contains("Invalid pattern quantifier", model.StatusMessage);
        Assert.Single(model.TreeNodes);
        Assert.Equal("Exception", model.TreeNodes[0].Header);
        Assert.Contains("[GeneratedRegex", model.CSharpSnippet);
    }

    [Fact]
    public void CreateEmptyModel_And_CreateLoadingModel_ReturnExpectedDefaults()
    {
        RegexDisplayModel emptyModel = RegexPresentationService.CreateEmptyModel("Custom empty message");
        Assert.Empty(emptyModel.TreeNodes);
        Assert.Equal("Custom empty message", emptyModel.StatusMessage);
        Assert.Equal(string.Empty, emptyModel.CSharpSnippet);

        RegexDisplayModel loadingModel = RegexPresentationService.CreateLoadingModel();
        Assert.Single(loadingModel.TreeNodes);
        Assert.Equal("Working...", loadingModel.TreeNodes[0].Header);
        Assert.Equal("Evaluating regular expression...", loadingModel.StatusMessage);
    }

    [Fact]
    public void UpdateExpansion_TogglesCapturesAndGroups()
    {
        var evalResult = RegexEvaluatorService.Evaluate(@"\w+", "test", "", RegexOptions.None);
        RegexDisplayModel model = RegexPresentationService.BuildModel(
            evalResult,
            @"\w+",
            "",
            RegexOptions.None,
            expandCaptures: false,
            expandGroups: false);

        MatchTreeNode matchNode = model.TreeNodes[0].Children[0];
        MatchTreeNode captures = matchNode.Children[0];
        MatchTreeNode groups = matchNode.Children[1];

        Assert.False(captures.IsExpanded);
        Assert.False(groups.IsExpanded);

        RegexPresentationService.UpdateExpansion(model.TreeNodes, expandCaptures: true, expandGroups: true);
        Assert.True(captures.IsExpanded);
        Assert.True(groups.IsExpanded);

        RegexPresentationService.UpdateExpansion(model.TreeNodes, expandCaptures: false, expandGroups: true);
        Assert.False(captures.IsExpanded);
        Assert.True(groups.IsExpanded);
    }

    [Fact]
    public void MatchTreeNode_PropertyNotification_FiresWhenExpandedChanges()
    {
        var node = new MatchTreeNode("Node", isExpanded: false);
        string? changedProperty = null;
        node.PropertyChanged += (sender, args) => changedProperty = args.PropertyName;

        node.IsExpanded = true;
        Assert.Equal(nameof(MatchTreeNode.IsExpanded), changedProperty);

        changedProperty = null;
        node.IsExpanded = true; // No change
        Assert.Null(changedProperty);

        Assert.Equal("Node", node.ToString());
    }
}
