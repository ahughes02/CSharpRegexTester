namespace CSharpRegexTester.Models;

/// <summary>
/// Represents a character index and length range within the test input string.
/// </summary>
public readonly record struct TextSpan(int Index, int Length);
