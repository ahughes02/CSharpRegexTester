using System.Text;
using System.Text.RegularExpressions;

namespace CSharpRegexTester.Services;

/// <summary>
/// Generates ready-to-paste C# code snippets for both [GeneratedRegex] source generation and runtime Regex.
/// </summary>
public static class CSharpCodeGenerator
{
    public static string GenerateSnippet(
        string pattern,
        string replacement,
        RegexOptions options,
        TimeSpan? matchTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(replacement);

        string patternLiteral = FormatStringLiteral(pattern);

        // [GeneratedRegex] is already compile-time generated, so RegexOptions.Compiled is redundant there (SYSLIB1045).
        RegexOptions generatedRegexOptions = options & ~RegexOptions.Compiled;
        string generatedOptionsArg;
        string runtimeOptionsArg;

        if (matchTimeout.HasValue)
        {
            int timeoutMs = Math.Max(1, (int)matchTimeout.Value.TotalMilliseconds);
            generatedOptionsArg = generatedRegexOptions == RegexOptions.None
                ? $", matchTimeoutMilliseconds: {timeoutMs}"
                : $", {FormatRegexOptionsExpression(generatedRegexOptions)}, matchTimeoutMilliseconds: {timeoutMs}";

            runtimeOptionsArg = options == RegexOptions.None
                ? $", RegexOptions.None, TimeSpan.FromMilliseconds({timeoutMs})"
                : $", {FormatRegexOptionsExpression(options)}, TimeSpan.FromMilliseconds({timeoutMs})";
        }
        else
        {
            generatedOptionsArg = generatedRegexOptions == RegexOptions.None
                ? string.Empty
                : $", {FormatRegexOptionsExpression(generatedRegexOptions)}";

            runtimeOptionsArg = options == RegexOptions.None
                ? string.Empty
                : $", {FormatRegexOptionsExpression(options)}";
        }

        var sb = new StringBuilder();
        sb.AppendLine("using System.Text.RegularExpressions;");
        sb.AppendLine();
        sb.AppendLine("// 1. Modern .NET Source-Generated Regex (compile-time):");
        sb.AppendLine($"[GeneratedRegex({patternLiteral}{generatedOptionsArg})]");
        sb.AppendLine("private static partial Regex MyRegex();");
        sb.AppendLine();
        sb.AppendLine("// 2. Runtime Regex instance:");
        sb.AppendLine($"var regex = new Regex({patternLiteral}{runtimeOptionsArg});");
        sb.AppendLine("MatchCollection matches = regex.Matches(input);");

        if (!string.IsNullOrEmpty(replacement))
        {
            string replacementLiteral = FormatStringLiteral(replacement);
            sb.AppendLine($"string replaced = regex.Replace(input, {replacementLiteral});");
        }

        return sb.ToString();
    }

    public static string FormatStringLiteral(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!value.Contains('"') && !value.Contains('\r') && !value.Contains('\n'))
        {
            return $"@\"{value}\"";
        }

        // Use C# 11+ raw string literal
        int maxConsecutiveQuotes = 2;
        int currentRun = 0;
        foreach (char c in value)
        {
            if (c == '"')
            {
                currentRun++;
                if (currentRun > maxConsecutiveQuotes)
                {
                    maxConsecutiveQuotes = currentRun;
                }
            }
            else
            {
                currentRun = 0;
            }
        }

        string delimiter = new('"', maxConsecutiveQuotes + 1);
        return $"{delimiter}\r\n{value}\r\n{delimiter}";
    }

    public static string FormatRegexOptionsExpression(RegexOptions options)
    {
        if (options == RegexOptions.None)
        {
            return "RegexOptions.None";
        }

        var parts = new List<string>();
        foreach (RegexOptions flag in Enum.GetValues<RegexOptions>())
        {
            if (flag != RegexOptions.None && options.HasFlag(flag))
            {
                parts.Add($"RegexOptions.{flag}");
            }
        }

        return string.Join(" | ", parts);
    }
}
