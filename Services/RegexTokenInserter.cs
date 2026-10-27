namespace CSharpRegexTester.Services;

public readonly record struct TokenInsertionResult(string UpdatedText, int NewCaretIndex);

/// <summary>
/// Inserts or wraps regex tokens at the active caret/selection position.
/// </summary>
public static class RegexTokenInserter
{
    public static TokenInsertionResult InsertToken(
        string currentText,
        int selectionStart,
        int selectionLength,
        string token)
    {
        ArgumentNullException.ThrowIfNull(currentText);
        ArgumentNullException.ThrowIfNull(token);

        int safeStart = Math.Clamp(selectionStart, 0, currentText.Length);
        int safeLength = Math.Clamp(selectionLength, 0, currentText.Length - safeStart);
        ReadOnlySpan<char> selectedText = currentText.AsSpan(safeStart, safeLength);

        string insertion;
        int newCaretOffset;

        // Wrapping group constructs: (), (?:), (?<name>), (?=), (?<=)
        if (token.StartsWith('(') && token.EndsWith(')'))
        {
            int openIndex = token.Length - 1;
            string prefix = token[..openIndex];
            insertion = $"{prefix}{selectedText})";
            newCaretOffset = safeLength > 0 ? insertion.Length : prefix.Length;
        }
        else if (token == "[^]")
        {
            insertion = $"[^{selectedText}]";
            newCaretOffset = safeLength > 0 ? insertion.Length : 2;
        }
        else
        {
            insertion = token;
            newCaretOffset = insertion.Length;
        }

        string updatedText = string.Concat(
            currentText.AsSpan(0, safeStart),
            insertion,
            currentText.AsSpan(safeStart + safeLength));

        return new TokenInsertionResult(updatedText, safeStart + newCaretOffset);
    }
}
