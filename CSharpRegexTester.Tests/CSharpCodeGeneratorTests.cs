using System.Text.RegularExpressions;
using CSharpRegexTester.Services;

namespace CSharpRegexTester.Tests;

public class CSharpCodeGeneratorTests
{
    [Fact]
    public void FormatStringLiteral_SimplePattern_UsesVerbatimLiteral()
    {
        string formatted = CSharpCodeGenerator.FormatStringLiteral(@"\d+\s+\w+");

        Assert.Equal(@"@""\d+\s+\w+""", formatted);
    }

    [Fact]
    public void FormatStringLiteral_WithQuotesOrNewlines_UsesRawStringLiteralWithEnoughQuotes()
    {
        string withSingleQuote = "hello \"world\"";
        string formattedSingle = CSharpCodeGenerator.FormatStringLiteral(withSingleQuote);
        Assert.StartsWith("\"\"\"\r\n", formattedSingle);
        Assert.EndsWith("\r\n\"\"\"", formattedSingle);

        string withThreeQuotes = "triple \"\"\" quotes";
        string formattedTriple = CSharpCodeGenerator.FormatStringLiteral(withThreeQuotes);
        Assert.StartsWith("\"\"\"\"\r\n", formattedTriple);
        Assert.EndsWith("\r\n\"\"\"\"", formattedTriple);
    }

    [Fact]
    public void FormatRegexOptionsExpression_FormatsNoneAndMultipleFlags()
    {
        Assert.Equal("RegexOptions.None", CSharpCodeGenerator.FormatRegexOptionsExpression(RegexOptions.None));

        string multi = CSharpCodeGenerator.FormatRegexOptionsExpression(
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.NonBacktracking);

        Assert.Contains("RegexOptions.IgnoreCase", multi);
        Assert.Contains("RegexOptions.Multiline", multi);
        Assert.Contains("RegexOptions.NonBacktracking", multi);
        Assert.Contains(" | ", multi);
    }

    [Fact]
    public void GenerateSnippet_WithAndWithoutReplacementAndOptions_ProducesExpectedCSharpCode()
    {
        string withoutReplacement = CSharpCodeGenerator.GenerateSnippet(@"\d+", string.Empty, RegexOptions.None);
        Assert.Contains("[GeneratedRegex(@\"\\d+\")]", withoutReplacement);
        Assert.DoesNotContain("regex.Replace", withoutReplacement);

        string withReplacement = CSharpCodeGenerator.GenerateSnippet(
            @"\d+",
            "#",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        Assert.Contains("[GeneratedRegex(@\"\\d+\", RegexOptions.IgnoreCase)]", withReplacement);
        Assert.Contains("var regex = new Regex(@\"\\d+\", RegexOptions.IgnoreCase | RegexOptions.Compiled);", withReplacement);
        Assert.Contains("string replaced = regex.Replace(input, @\"#\");", withReplacement);
    }
}
