using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace CSharpRegexTester;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);

    private bool stopWork;
    private CancellationTokenSource? regexWorkCts;
    private RegexMatchHighlightAdorner? highlightAdorner;
    private readonly List<string> savedExpressions = [];

    private sealed record CaptureSnapshot(string Value, int Index, int Length);
    private sealed record GroupSnapshot(int Number, string Name, bool Success, string Value, int Index, int Length);
    private sealed record MatchSnapshot(int Index, int Length, string Value, IReadOnlyList<CaptureSnapshot> Captures, IReadOnlyList<GroupSnapshot> Groups);
    private sealed record RegexWorkResult(
        IReadOnlyList<MatchSnapshot> Matches,
        string ReplacementResults,
        IReadOnlyList<string> SplitResults,
        TimeSpan Elapsed);

    public MainWindow()
    {
        InitializeComponent();

        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (AdornerLayer.GetAdornerLayer(DataTextBox) is { } adornerLayer)
        {
            highlightAdorner = new RegexMatchHighlightAdorner(DataTextBox);
            adornerLayer.Add(highlightAdorner);
        }

        stopWork = true;
        var configFile = RegexTesterConfigurationFile.Load();

        if (configFile is not null)
        {
            DataTextBox.Text = configFile.CurrentTestData ?? string.Empty;
            ExpressionTextBox.Text = configFile.CurrentExpression ?? string.Empty;
            ReplacementTextTextBox.Text = configFile.CurrentReplacementText ?? string.Empty;
            ApplyRegexOptionsToCheckBoxes(configFile.SelectedOptions);
            ExpandCapturesCheckBox.IsChecked = configFile.ExpandCaptures;
            ExpandGroupsCheckBox.IsChecked = configFile.ExpandGroups;

            if (configFile.ExpressionList is { Count: > 0 })
            {
                foreach (string expr in configFile.ExpressionList.Where(s => !string.IsNullOrEmpty(s)).Distinct())
                {
                    savedExpressions.Add(expr);
                }
                RefreshSavedExpressionsComboBox();
            }
        }
        stopWork = false;

        UpdateCSharpSnippet();
        _ = SetupAndRunRegexWorkerAsync();
    }

    /// <summary>
    /// Text Changed Handler for ExpressionTextBox, DataTextBox and, ReplacementTextTextBox
    /// </summary>
    private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (stopWork)
        {
            return;
        }

        UpdateCSharpSnippet();
        _ = SetupAndRunRegexWorkerAsync();
    }

    /// <summary>
    /// Check Changed Handler for options checkboxes
    /// </summary>
    private void Checkbox_CheckChanged(object sender, RoutedEventArgs e)
    {
        if (stopWork)
        {
            return;
        }

        UpdateCSharpSnippet();
        _ = SetupAndRunRegexWorkerAsync();
    }

    private void ClearAllButton_Click(object sender, RoutedEventArgs e)
    {
        stopWork = true;
        regexWorkCts?.Cancel();

        DataTextBox.Text = string.Empty;
        ExpressionTextBox.Text = string.Empty;
        ReplacementTextTextBox.Text = string.Empty;
        ReplacementResultsTextBox.Text = string.Empty;

        ApplyRegexOptionsToCheckBoxes(RegexOptions.None);
        ExpandCapturesCheckBox.IsChecked = false;
        ExpandGroupsCheckBox.IsChecked = false;

        ClearResultsDisplay("Cleared all inputs and options.");
        UpdateCSharpSnippet();

        stopWork = false;
    }

    private void RegexCharacterButton_Click(object sender, RoutedEventArgs e)
    {
        if (e.Source is not Button button)
        {
            return;
        }

        string token = button.Tag as string ?? button.Content as string ?? string.Empty;
        if (string.IsNullOrEmpty(token))
        {
            return;
        }

        int selectionStart = ExpressionTextBox.SelectionStart;
        int selectionLength = ExpressionTextBox.SelectionLength;
        string selectedText = ExpressionTextBox.SelectedText;

        string insertion;
        int newCaretOffset;

        // If the token is a wrapping construct like (), (?:), (?<name>), (?=), (?<=)
        if (token.EndsWith(')') && token.StartsWith('('))
        {
            int openIndex = token.Length - 1;
            string prefix = token[..openIndex];
            insertion = $"{prefix}{selectedText})";
            newCaretOffset = selectionLength > 0 ? insertion.Length : prefix.Length;
        }
        else if (token == "[^]")
        {
            insertion = $"[^{selectedText}]";
            newCaretOffset = selectionLength > 0 ? insertion.Length : 2;
        }
        else
        {
            insertion = token;
            newCaretOffset = insertion.Length;
        }

        string currentText = ExpressionTextBox.Text;
        ExpressionTextBox.Text = string.Concat(
            currentText.AsSpan(0, selectionStart),
            insertion,
            currentText.AsSpan(selectionStart + selectionLength));

        ExpressionTextBox.SelectionStart = selectionStart + newCaretOffset;
        ExpressionTextBox.SelectionLength = 0;
        ExpressionTextBox.Focus();
    }

    private void SaveExpressionButton_Click(object sender, RoutedEventArgs e)
    {
        string expr = ExpressionTextBox.Text;
        if (string.IsNullOrEmpty(expr))
        {
            StatusTextBlock.Text = "Enter a regular expression before saving.";
            return;
        }

        if (!savedExpressions.Contains(expr))
        {
            savedExpressions.Insert(0, expr);
            RefreshSavedExpressionsComboBox();
        }

        SavedExpressionsComboBox.SelectedItem = expr;
        SaveConfiguration();
        StatusTextBlock.Text = "Saved pattern to history.";
    }

    private void RemoveSavedExpressionButton_Click(object sender, RoutedEventArgs e)
    {
        if (SavedExpressionsComboBox.SelectedItem is string selected && savedExpressions.Remove(selected))
        {
            RefreshSavedExpressionsComboBox();
            SaveConfiguration();
            StatusTextBlock.Text = "Removed pattern from saved list.";
        }
    }

    private void SavedExpressionsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (stopWork || SavedExpressionsComboBox.SelectedItem is not string selected)
        {
            return;
        }

        ExpressionTextBox.Text = selected;
        ExpressionTextBox.CaretIndex = selected.Length;
    }

    private void RefreshSavedExpressionsComboBox()
    {
        bool previousStopWork = stopWork;
        stopWork = true;
        SavedExpressionsComboBox.ItemsSource = null;
        SavedExpressionsComboBox.ItemsSource = savedExpressions;
        stopWork = previousStopWork;
    }

    private void CopyCSharpButton_Click(object sender, RoutedEventArgs e)
    {
        string snippet = GenerateCSharpSnippet(ExpressionTextBox.Text, ReplacementTextTextBox.Text, GetSelectedRegexOptions());
        CSharpSnippetTextBox.Text = snippet;

        try
        {
            Clipboard.SetText(snippet);
            StatusTextBlock.Text = "Copied C# [GeneratedRegex] & Regex snippet to clipboard.";
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Unable to copy to clipboard: {ex.Message}";
        }
    }

    private void SaveAndExitButton_Click(object sender, RoutedEventArgs e)
    {
        SaveConfiguration();
        Application.Current.Shutdown();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SaveConfiguration();
    }

    private void SaveConfiguration()
    {
        if (!string.IsNullOrEmpty(ExpressionTextBox.Text) && !savedExpressions.Contains(ExpressionTextBox.Text))
        {
            savedExpressions.Insert(0, ExpressionTextBox.Text);
        }

        var configFile = new RegexTesterConfigurationFile
        {
            CurrentExpression = ExpressionTextBox.Text,
            CurrentReplacementText = ReplacementTextTextBox.Text,
            CurrentTestData = DataTextBox.Text,
            SelectedOptions = GetSelectedRegexOptions(),
            ExpandCaptures = ExpandCapturesCheckBox.IsChecked == true,
            ExpandGroups = ExpandGroupsCheckBox.IsChecked == true,
            ExpressionList = [.. savedExpressions]
        };
        configFile.Save();
    }

    private RegexOptions GetSelectedRegexOptions()
    {
        RegexOptions options = RegexOptions.None;

        if (IgnoreCaseCheckBox.IsChecked == true)
        {
            options |= RegexOptions.IgnoreCase;
        }

        if (ExplicitCaptureCheckBox.IsChecked == true)
        {
            options |= RegexOptions.ExplicitCapture;
        }

        if (CompiledCheckBox.IsChecked == true)
        {
            options |= RegexOptions.Compiled;
        }

        if (CultureInvariantCheckBox.IsChecked == true)
        {
            options |= RegexOptions.CultureInvariant;
        }

        if (ECMAScriptCheckBox.IsChecked == true)
        {
            options |= RegexOptions.ECMAScript;
        }

        if (IgnorePatternWhitespace.IsChecked == true)
        {
            options |= RegexOptions.IgnorePatternWhitespace;
        }

        if (MultilineCheckBox.IsChecked == true)
        {
            options |= RegexOptions.Multiline;
        }

        if (RightToLeftCheckBox.IsChecked == true)
        {
            options |= RegexOptions.RightToLeft;
        }

        if (SinglelineCheckBox.IsChecked == true)
        {
            options |= RegexOptions.Singleline;
        }

        if (NonBacktrackingCheckBox.IsChecked == true)
        {
            options |= RegexOptions.NonBacktracking;
        }

        return options;
    }

    private void ApplyRegexOptionsToCheckBoxes(RegexOptions options)
    {
        IgnoreCaseCheckBox.IsChecked = options.HasFlag(RegexOptions.IgnoreCase);
        ExplicitCaptureCheckBox.IsChecked = options.HasFlag(RegexOptions.ExplicitCapture);
        CompiledCheckBox.IsChecked = options.HasFlag(RegexOptions.Compiled);
        CultureInvariantCheckBox.IsChecked = options.HasFlag(RegexOptions.CultureInvariant);
        ECMAScriptCheckBox.IsChecked = options.HasFlag(RegexOptions.ECMAScript);
        IgnorePatternWhitespace.IsChecked = options.HasFlag(RegexOptions.IgnorePatternWhitespace);
        MultilineCheckBox.IsChecked = options.HasFlag(RegexOptions.Multiline);
        RightToLeftCheckBox.IsChecked = options.HasFlag(RegexOptions.RightToLeft);
        SinglelineCheckBox.IsChecked = options.HasFlag(RegexOptions.Singleline);
        NonBacktrackingCheckBox.IsChecked = options.HasFlag(RegexOptions.NonBacktracking);
    }

    private async Task SetupAndRunRegexWorkerAsync()
    {
        if (!IsLoaded || stopWork)
        {
            return;
        }

        regexWorkCts?.Cancel();
        regexWorkCts?.Dispose();
        var cts = new CancellationTokenSource();
        regexWorkCts = cts;
        CancellationToken token = cts.Token;

        string expression = ExpressionTextBox.Text;
        string testData = DataTextBox.Text;
        string replacementText = ReplacementTextTextBox.Text;
        RegexOptions options = GetSelectedRegexOptions();

        if (string.IsNullOrEmpty(expression) || string.IsNullOrEmpty(testData))
        {
            ClearResultsDisplay("Enter both a regular expression and test data to evaluate.");
            return;
        }

        MatchesTreeView.Items.Clear();
        MatchesTreeView.Items.Add("Working...");
        StatusTextBlock.Text = "Evaluating regular expression...";

        try
        {
            RegexWorkResult result = await Task.Run(
                () => EvaluateRegex(expression, testData, replacementText, options, token),
                token);

            if (token.IsCancellationRequested)
            {
                return;
            }

            DisplayResults(result);
        }
        catch (OperationCanceledException)
        {
            // Cancelled by a newer input change.
        }
        catch (RegexMatchTimeoutException timeoutEx)
        {
            if (!token.IsCancellationRequested)
            {
                DisplayTimeoutException(timeoutEx);
            }
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                DisplayException(ex);
            }
        }
    }

    private static RegexWorkResult EvaluateRegex(
        string pattern,
        string input,
        string replacement,
        RegexOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        long startTimestamp = Stopwatch.GetTimestamp();

        string replaceWith = replacement ?? string.Empty;
        var regex = new Regex(pattern, options, RegexTimeout);
        MatchCollection matches = regex.Matches(input);

        var matchSnapshots = new List<MatchSnapshot>();

        for (int i = 0; i < matches.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Match match = matches[i];

            var captures = new List<CaptureSnapshot>(match.Captures.Count);
            for (int j = 0; j < match.Captures.Count; j++)
            {
                Capture capture = match.Captures[j];
                captures.Add(new CaptureSnapshot(capture.Value, capture.Index, capture.Length));
            }

            var groups = new List<GroupSnapshot>(match.Groups.Count);
            for (int j = 0; j < match.Groups.Count; j++)
            {
                Group group = match.Groups[j];
                string rawGroupName = regex.GroupNameFromNumber(j);
                string formattedName = rawGroupName != j.ToString() ? $"<{rawGroupName}>" : string.Empty;
                groups.Add(new GroupSnapshot(j, formattedName, group.Success, group.Value, group.Index, group.Length));
            }

            matchSnapshots.Add(new MatchSnapshot(match.Index, match.Length, match.Value, captures, groups));
        }

        string replacementResults = matchSnapshots.Count > 0
            ? regex.Replace(input, replaceWith)
            : string.Empty;

        string[] splitResults = regex.Split(input);
        TimeSpan elapsed = Stopwatch.GetElapsedTime(startTimestamp);

        return new RegexWorkResult(matchSnapshots, replacementResults, splitResults, elapsed);
    }

    private void ClearResultsDisplay(string statusMessage)
    {
        MatchesTreeView.Items.Clear();
        ReplacementResultsTextBox.Text = string.Empty;
        SplitResultsListBox.ItemsSource = null;
        MatchSummaryTextBlock.Text = string.Empty;
        StatusTextBlock.Text = statusMessage;
        highlightAdorner?.UpdateHighlights([]);
    }

    private void DisplayResults(RegexWorkResult result)
    {
        // Populate Split Results
        var formattedSplits = new List<string>(result.SplitResults.Count);
        for (int i = 0; i < result.SplitResults.Count; i++)
        {
            string part = result.SplitResults[i];
            formattedSplits.Add($"[{i}] \"{part}\" ({part.Length} chars)");
        }
        SplitResultsListBox.ItemsSource = formattedSplits;

        double elapsedMs = result.Elapsed.TotalMilliseconds;

        if (result.Matches.Count > 0)
        {
            var matchesTreeItem = new TreeViewItem { Header = $"Matches ({result.Matches.Count})" };
            var highlightSpans = new List<TextSpan>(result.Matches.Count);

            for (int i = 0; i < result.Matches.Count; i++)
            {
                MatchSnapshot match = result.Matches[i];
                var matchSpan = new TextSpan(match.Index, match.Length);
                highlightSpans.Add(matchSpan);

                var matchTreeItem = new TreeViewItem
                {
                    Header = $"[{i}] - [{match.Value}] (Index: {match.Index}, Length: {match.Length})",
                    Tag = matchSpan
                };
                matchesTreeItem.Items.Add(matchTreeItem);

                // Captures
                var capturesTreeItem = new TreeViewItem { Header = $"Captures ({match.Captures.Count})" };
                matchTreeItem.Items.Add(capturesTreeItem);
                for (int j = 0; j < match.Captures.Count; j++)
                {
                    CaptureSnapshot capture = match.Captures[j];
                    var captureTreeItem = new TreeViewItem
                    {
                        Header = $"[{j}] - [{capture.Value}] (Index: {capture.Index}, Length: {capture.Length})",
                        Tag = new TextSpan(capture.Index, capture.Length)
                    };
                    capturesTreeItem.Items.Add(captureTreeItem);
                }

                // Groups
                var groupsTreeItem = new TreeViewItem { Header = $"Groups ({match.Groups.Count})" };
                matchTreeItem.Items.Add(groupsTreeItem);
                for (int j = 0; j < match.Groups.Count; j++)
                {
                    GroupSnapshot group = match.Groups[j];
                    string header = group.Success
                        ? $"[{group.Number}]{group.Name} - [{group.Value}] (Index: {group.Index}, Length: {group.Length})"
                        : $"[{group.Number}]{group.Name} - <not matched>";

                    var groupTreeItem = new TreeViewItem
                    {
                        Header = header,
                        Tag = group.Success ? new TextSpan(group.Index, group.Length) : null
                    };
                    groupsTreeItem.Items.Add(groupTreeItem);
                }

                if (ExpandCapturesCheckBox.IsChecked == true)
                {
                    capturesTreeItem.IsExpanded = true;
                }

                if (ExpandGroupsCheckBox.IsChecked == true)
                {
                    groupsTreeItem.IsExpanded = true;
                }

                matchTreeItem.IsExpanded = true;
            }

            matchesTreeItem.IsExpanded = true;

            MatchesTreeView.Items.Clear();
            MatchesTreeView.Items.Add(matchesTreeItem);
            ReplacementResultsTextBox.Text = result.ReplacementResults;

            string matchLabel = result.Matches.Count == 1 ? "1 match" : $"{result.Matches.Count} matches";
            MatchSummaryTextBlock.Text = $"{matchLabel} ({elapsedMs:F2} ms)";
            StatusTextBlock.Text = $"Found {matchLabel} and {result.SplitResults.Count} split segment(s) in {elapsedMs:F2} ms. Click any match/group in the tree to locate it.";
            highlightAdorner?.UpdateHighlights(highlightSpans);
        }
        else
        {
            MatchesTreeView.Items.Clear();
            MatchesTreeView.Items.Add("No Matches");
            ReplacementResultsTextBox.Text = string.Empty;
            MatchSummaryTextBlock.Text = $"0 matches ({elapsedMs:F2} ms)";
            StatusTextBlock.Text = $"No matches found ({elapsedMs:F2} ms).";
            highlightAdorner?.UpdateHighlights([]);
        }
    }

    private void MatchesTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TreeViewItem { Tag: TextSpan span } &&
            span.Index >= 0 &&
            span.Index + span.Length <= DataTextBox.Text.Length)
        {
            highlightAdorner?.SetSelectedSpan(span);
            int lineIndex = DataTextBox.GetLineIndexFromCharacterIndex(span.Index);
            if (lineIndex >= 0)
            {
                DataTextBox.ScrollToLine(lineIndex);
            }
            StatusTextBlock.Text = $"Selected span: Index {span.Index}, Length {span.Length}";
        }
        else
        {
            highlightAdorner?.SetSelectedSpan(null);
        }
    }

    private void DisplayTimeoutException(RegexMatchTimeoutException ex)
    {
        MatchesTreeView.Items.Clear();

        var treeItem = new TreeViewItem { Header = "Regex Timeout (Catastrophic Backtracking Protection)" };
        treeItem.Items.Add(new TreeViewItem { Header = $"Timed out after {ex.MatchTimeout.TotalSeconds:F1}s." });
        treeItem.Items.Add(new TreeViewItem { Header = "Tip: Try enabling 'NonBacktracking' or simplifying nested quantifiers." });
        treeItem.IsExpanded = true;

        MatchesTreeView.Items.Add(treeItem);
        MatchSummaryTextBlock.Text = "Timed out";
        StatusTextBlock.Text = $"Regex evaluation timed out after {ex.MatchTimeout.TotalSeconds:F1} seconds.";
        highlightAdorner?.UpdateHighlights([]);
    }

    private void DisplayException(Exception ex)
    {
        MatchesTreeView.Items.Clear();

        var treeItem = new TreeViewItem { Header = "Exception" };
        treeItem.Items.Add(new TreeViewItem { Header = $"Message: {ex.Message}" });
        treeItem.Items.Add(new TreeViewItem { Header = $"Source: {ex.Source}" });
        treeItem.IsExpanded = true;

        MatchesTreeView.Items.Add(treeItem);
        MatchSummaryTextBlock.Text = "Invalid Pattern";
        StatusTextBlock.Text = $"Error: {ex.Message}";
        highlightAdorner?.UpdateHighlights([]);
    }

    private void UpdateCSharpSnippet()
    {
        if (CSharpSnippetTextBox is null)
        {
            return;
        }

        CSharpSnippetTextBox.Text = GenerateCSharpSnippet(
            ExpressionTextBox.Text,
            ReplacementTextTextBox.Text,
            GetSelectedRegexOptions());
    }

    private static string GenerateCSharpSnippet(string pattern, string replacement, RegexOptions options)
    {
        string patternLiteral = FormatCSharpStringLiteral(pattern);
        string optionsExpression = FormatRegexOptionsExpression(options);
        string optionsArgument = options == RegexOptions.None ? string.Empty : $", {optionsExpression}";

        var sb = new StringBuilder();
        sb.AppendLine("using System.Text.RegularExpressions;");
        sb.AppendLine();
        sb.AppendLine("// 1. Modern .NET Source-Generated Regex (compile-time):");
        sb.AppendLine($"[GeneratedRegex({patternLiteral}{optionsArgument})]");
        sb.AppendLine("private static partial Regex MyRegex();");
        sb.AppendLine();
        sb.AppendLine("// 2. Runtime Regex instance:");
        sb.AppendLine($"var regex = new Regex({patternLiteral}{optionsArgument});");
        sb.AppendLine("MatchCollection matches = regex.Matches(input);");

        if (!string.IsNullOrEmpty(replacement))
        {
            string replacementLiteral = FormatCSharpStringLiteral(replacement);
            sb.AppendLine($"string replaced = regex.Replace(input, {replacementLiteral});");
        }

        return sb.ToString();
    }

    private static string FormatCSharpStringLiteral(string value)
    {
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

    private static string FormatRegexOptionsExpression(RegexOptions options)
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
