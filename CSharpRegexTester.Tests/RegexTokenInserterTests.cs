using CSharpRegexTester.Services;

namespace CSharpRegexTester.Tests;

public class RegexTokenInserterTests
{
    [Fact]
    public void InsertToken_SimpleToken_InsertsAtCaretAndAdvancesCursor()
    {
        TokenInsertionResult result = RegexTokenInserter.InsertToken("abc", 1, 0, @"\d");

        Assert.Equal(@"a\dbc", result.UpdatedText);
        Assert.Equal(3, result.NewCaretIndex);
    }

    [Fact]
    public void InsertToken_GroupTokenWithSelection_WrapsSelectedText()
    {
        TokenInsertionResult result = RegexTokenInserter.InsertToken(@"foo\d+bar", 3, 3, "(?<num>)");

        Assert.Equal(@"foo(?<num>\d+)bar", result.UpdatedText);
        Assert.Equal(14, result.NewCaretIndex);
    }

    [Fact]
    public void InsertToken_GroupTokenWithoutSelection_PlacesCaretInsideGroup()
    {
        TokenInsertionResult result = RegexTokenInserter.InsertToken("abcdef", 3, 0, "()");

        Assert.Equal("abc()def", result.UpdatedText);
        Assert.Equal(4, result.NewCaretIndex);
    }

    [Fact]
    public void InsertToken_NegatedCharacterClass_WrapsOrPlacesCaretInsideBrackets()
    {
        TokenInsertionResult emptySelection = RegexTokenInserter.InsertToken("abc", 0, 0, "[^]");
        Assert.Equal("[^]abc", emptySelection.UpdatedText);
        Assert.Equal(2, emptySelection.NewCaretIndex);

        TokenInsertionResult withSelection = RegexTokenInserter.InsertToken("0-9", 0, 3, "[^]");
        Assert.Equal("[^0-9]", withSelection.UpdatedText);
        Assert.Equal(6, withSelection.NewCaretIndex);
    }

    [Fact]
    public void InsertToken_OutOfRangeSelection_ClampsSafely()
    {
        TokenInsertionResult result = RegexTokenInserter.InsertToken("hi", 50, 50, "^");

        Assert.Equal("hi^", result.UpdatedText);
        Assert.Equal(3, result.NewCaretIndex);
    }
}
