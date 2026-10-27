using System.Text.RegularExpressions;

namespace CSharpRegexTester.Models;

/// <summary>
/// Persisted user configuration and session state for CSharpRegexTester.
/// </summary>
public sealed class AppSettings
{
    public List<string> ExpressionList { get; set; } = [];
    public string CurrentExpression { get; set; } = string.Empty;
    public string CurrentReplacementText { get; set; } = string.Empty;
    public string CurrentTestData { get; set; } = string.Empty;
    public RegexOptions SelectedOptions { get; set; } = RegexOptions.None;
    public bool ExpandCaptures { get; set; }
    public bool ExpandGroups { get; set; }
    public double TimeoutSeconds { get; set; } = 2.0;
}
