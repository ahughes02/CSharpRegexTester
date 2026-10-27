using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using CSharpRegexTester.Controls;
using CSharpRegexTester.Models;
using CSharpRegexTester.Services;

namespace CSharpRegexTester;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly (CheckBox CheckBox, RegexOptions Option)[] optionMappings;
    private readonly List<string> savedExpressions = [];

    private bool stopWork = true;
    private CancellationTokenSource? regexWorkCts;
    private RegexMatchHighlightAdorner? highlightAdorner;

    public MainWindow()
    {
        InitializeComponent();

        optionMappings =
        [
            (IgnoreCaseCheckBox, RegexOptions.IgnoreCase),
            (ExplicitCaptureCheckBox, RegexOptions.ExplicitCapture),
            (CompiledCheckBox, RegexOptions.Compiled),
            (CultureInvariantCheckBox, RegexOptions.CultureInvariant),
            (ECMAScriptCheckBox, RegexOptions.ECMAScript),
            (IgnorePatternWhitespace, RegexOptions.IgnorePatternWhitespace),
            (MultilineCheckBox, RegexOptions.Multiline),
            (RightToLeftCheckBox, RegexOptions.RightToLeft),
            (SinglelineCheckBox, RegexOptions.Singleline),
            (NonBacktrackingCheckBox, RegexOptions.NonBacktracking)
        ];

        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (AdornerLayer.GetAdornerLayer(DataTextBox) is { } adornerLayer)
        {
            highlightAdorner = new RegexMatchHighlightAdorner(DataTextBox);
            adornerLayer.Add(highlightAdorner);
        }

        LoadConfigurationIntoUi();
        UpdateCSharpSnippet();
        _ = SetupAndRunRegexWorkerAsync();
    }

    private void LoadConfigurationIntoUi()
    {
        stopWork = true;
        AppSettings? settings = SettingsService.Load();

        if (settings is not null)
        {
            DataTextBox.Text = settings.CurrentTestData ?? string.Empty;
            ExpressionTextBox.Text = settings.CurrentExpression ?? string.Empty;
            ReplacementTextTextBox.Text = settings.CurrentReplacementText ?? string.Empty;
            ApplyRegexOptionsToCheckBoxes(settings.SelectedOptions);
            ExpandCapturesCheckBox.IsChecked = settings.ExpandCaptures;
            ExpandGroupsCheckBox.IsChecked = settings.ExpandGroups;

            if (settings.ExpressionList is { Count: > 0 })
            {
                foreach (string expr in settings.ExpressionList.Where(s => !string.IsNullOrEmpty(s)).Distinct())
                {
                    savedExpressions.Add(expr);
                }
                RefreshSavedExpressionsComboBox();
            }
        }
        stopWork = false;
    }

    /// <summary>
    /// Text Changed Handler for ExpressionTextBox, DataTextBox, and ReplacementTextTextBox.
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
    /// Check Changed Handler for options and tree expansion checkboxes.
    /// </summary>
    private void Checkbox_CheckChanged(object sender, RoutedEventArgs e)
    {
        if (stopWork)
        {
            return;
        }

        if (ReferenceEquals(sender, ExpandCapturesCheckBox) || ReferenceEquals(sender, ExpandGroupsCheckBox))
        {
            UpdateTreeExpansionInPlace();
            return;
        }

        UpdateCSharpSnippet();
        _ = SetupAndRunRegexWorkerAsync();
    }

    private void UpdateTreeExpansionInPlace()
    {
        if (MatchesTreeView.Items.Count == 0 || MatchesTreeView.Items[0] is not TreeViewItem rootItem)
        {
            return;
        }

        bool expandCaptures = ExpandCapturesCheckBox.IsChecked == true;
        bool expandGroups = ExpandGroupsCheckBox.IsChecked == true;

        foreach (TreeViewItem matchItem in rootItem.Items.OfType<TreeViewItem>())
        {
            if (matchItem.Items.Count >= 1 && matchItem.Items[0] is TreeViewItem capturesItem)
            {
                capturesItem.IsExpanded = expandCaptures;
            }

            if (matchItem.Items.Count >= 2 && matchItem.Items[1] is TreeViewItem groupsItem)
            {
                groupsItem.IsExpanded = expandGroups;
            }
        }
    }

    private void ClearAllButton_Click(object sender, RoutedEventArgs e)
    {
        stopWork = true;
        CancelPendingEvaluation();

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

        TokenInsertionResult insertion = RegexTokenInserter.InsertToken(
            ExpressionTextBox.Text,
            ExpressionTextBox.SelectionStart,
            ExpressionTextBox.SelectionLength,
            token);

        ExpressionTextBox.Text = insertion.UpdatedText;
        ExpressionTextBox.SelectionStart = insertion.NewCaretIndex;
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
        string snippet = CSharpCodeGenerator.GenerateSnippet(
            ExpressionTextBox.Text,
            ReplacementTextTextBox.Text,
            GetSelectedRegexOptions());
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
        Close();
        Application.Current?.Shutdown();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        CancelPendingEvaluation();
        SaveConfiguration();
    }

    private void SaveConfiguration()
    {
        var settings = new AppSettings
        {
            CurrentExpression = ExpressionTextBox.Text,
            CurrentReplacementText = ReplacementTextTextBox.Text,
            CurrentTestData = DataTextBox.Text,
            SelectedOptions = GetSelectedRegexOptions(),
            ExpandCaptures = ExpandCapturesCheckBox.IsChecked == true,
            ExpandGroups = ExpandGroupsCheckBox.IsChecked == true,
            ExpressionList = [.. savedExpressions]
        };
        SettingsService.Save(settings);
    }

    private RegexOptions GetSelectedRegexOptions()
    {
        RegexOptions options = RegexOptions.None;
        foreach ((CheckBox checkBox, RegexOptions option) in optionMappings)
        {
            if (checkBox.IsChecked == true)
            {
                options |= option;
            }
        }
        return options;
    }

    private void ApplyRegexOptionsToCheckBoxes(RegexOptions options)
    {
        foreach ((CheckBox checkBox, RegexOptions option) in optionMappings)
        {
            checkBox.IsChecked = options.HasFlag(option);
        }
    }

    private void CancelPendingEvaluation()
    {
        if (regexWorkCts is { } activeCts)
        {
            regexWorkCts = null;
            activeCts.Cancel();
            activeCts.Dispose();
        }
    }

    private async Task SetupAndRunRegexWorkerAsync()
    {
        if (stopWork)
        {
            return;
        }

        CancelPendingEvaluation();
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
        highlightAdorner?.UpdateHighlights([]);

        try
        {
            RegexEvaluationResult result = await Task.Run(
                () => RegexEvaluatorService.Evaluate(expression, testData, replacementText, options, token),
                token);

            if (token.IsCancellationRequested || !ReferenceEquals(regexWorkCts, cts))
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
            if (!token.IsCancellationRequested && ReferenceEquals(regexWorkCts, cts))
            {
                DisplayTimeoutException(timeoutEx);
            }
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested && ReferenceEquals(regexWorkCts, cts))
            {
                DisplayException(ex);
            }
        }
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

    private void DisplayResults(RegexEvaluationResult result)
    {
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
                highlightSpans.Add(match.Span);

                var matchTreeItem = new TreeViewItem
                {
                    Header = $"[{i}] - [{match.Value}] (Index: {match.Index}, Length: {match.Length})",
                    Tag = match.Span
                };
                matchesTreeItem.Items.Add(matchTreeItem);

                // Captures
                var capturesTreeItem = new TreeViewItem
                {
                    Header = $"Captures ({match.Captures.Count})",
                    IsExpanded = ExpandCapturesCheckBox.IsChecked == true
                };
                matchTreeItem.Items.Add(capturesTreeItem);
                for (int j = 0; j < match.Captures.Count; j++)
                {
                    CaptureSnapshot capture = match.Captures[j];
                    capturesTreeItem.Items.Add(new TreeViewItem
                    {
                        Header = $"[{j}] - [{capture.Value}] (Index: {capture.Index}, Length: {capture.Length})",
                        Tag = capture.Span
                    });
                }

                // Groups
                var groupsTreeItem = new TreeViewItem
                {
                    Header = $"Groups ({match.Groups.Count})",
                    IsExpanded = ExpandGroupsCheckBox.IsChecked == true
                };
                matchTreeItem.Items.Add(groupsTreeItem);
                for (int j = 0; j < match.Groups.Count; j++)
                {
                    GroupSnapshot group = match.Groups[j];
                    string header = group.Success
                        ? $"[{group.Number}]{group.Name} - [{group.Value}] (Index: {group.Index}, Length: {group.Length})"
                        : $"[{group.Number}]{group.Name} - <not matched>";

                    groupsTreeItem.Items.Add(new TreeViewItem
                    {
                        Header = header,
                        Tag = group.Span
                    });
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

        CSharpSnippetTextBox.Text = CSharpCodeGenerator.GenerateSnippet(
            ExpressionTextBox.Text,
            ReplacementTextTextBox.Text,
            GetSelectedRegexOptions());
    }
}
