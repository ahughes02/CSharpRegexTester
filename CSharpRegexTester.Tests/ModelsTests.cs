using System.Text.RegularExpressions;
using CSharpRegexTester.Models;

namespace CSharpRegexTester.Tests;

public class ModelsTests
{
    [Fact]
    public void TextSpan_Properties_AreSetCorrectly()
    {
        var span = new TextSpan(5, 12);

        Assert.Equal(5, span.Index);
        Assert.Equal(12, span.Length);
    }

    [Fact]
    public void CaptureSnapshot_Span_ReturnsMatchingTextSpan()
    {
        var capture = new CaptureSnapshot("abc", 3, 3);

        Assert.Equal("abc", capture.Value);
        Assert.Equal(new TextSpan(3, 3), capture.Span);
    }

    [Fact]
    public void GroupSnapshot_Span_ReturnsSpanWhenSuccessAndNullWhenUnsuccessful()
    {
        var matchedGroup = new GroupSnapshot(1, "<word>", true, "hello", 0, 5);
        var unmatchedGroup = new GroupSnapshot(2, "<opt>", false, string.Empty, 0, 0);

        Assert.Equal(new TextSpan(0, 5), matchedGroup.Span);
        Assert.Null(unmatchedGroup.Span);
        Assert.Equal(1, matchedGroup.Number);
        Assert.Equal("<word>", matchedGroup.Name);
        Assert.Equal("hello", matchedGroup.Value);
    }

    [Fact]
    public void MatchSnapshot_And_RegexEvaluationResult_ExposeExpectedProperties()
    {
        var capture = new CaptureSnapshot("test", 2, 4);
        var group = new GroupSnapshot(0, string.Empty, true, "test", 2, 4);
        var match = new MatchSnapshot(2, 4, "test", [capture], [group]);
        var result = new RegexEvaluationResult([match], "replaced", ["a", "b"], TimeSpan.FromMilliseconds(5));

        Assert.Equal(new TextSpan(2, 4), match.Span);
        Assert.Single(match.Captures);
        Assert.Single(match.Groups);
        Assert.Single(result.Matches);
        Assert.Equal("replaced", result.ReplacementResults);
        Assert.Equal(["a", "b"], result.SplitResults);
        Assert.Equal(TimeSpan.FromMilliseconds(5), result.Elapsed);
    }

    [Fact]
    public void AppSettings_DefaultValues_AreInitializedCleanly()
    {
        var settings = new AppSettings();

        Assert.Empty(settings.ExpressionList);
        Assert.Equal(string.Empty, settings.CurrentExpression);
        Assert.Equal(string.Empty, settings.CurrentReplacementText);
        Assert.Equal(string.Empty, settings.CurrentTestData);
        Assert.Equal(RegexOptions.None, settings.SelectedOptions);
        Assert.False(settings.ExpandCaptures);
        Assert.False(settings.ExpandGroups);
        Assert.Equal(2.0, settings.TimeoutSeconds);
    }
}
