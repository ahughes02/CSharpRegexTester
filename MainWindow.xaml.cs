using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using CSharpRegexTester.Controls;
using CSharpRegexTester.Models;
using CSharpRegexTester.Services;

namespace CSharpRegexTester;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public const int MaxTreeMatches = RegexPresentationService.MaxTreeMatches;
    public const int MaxSplitDisplayItems = RegexPresentationService.MaxSplitDisplayItems;

    private readonly (CheckBox CheckBox, RegexOptions Option)[] optionMappings;
    private readonly List<string> savedExpressions = [];
    private readonly DispatcherTimer debounceTimer;

    private bool stopWork = true;
    private CancellationTokenSource? regexWorkCts;
    private RegexMatchHighlightAdorner? highlightAdorner;

    public MainWindow()
    {
        InitializeComponent();

        debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        debounceTimer.Tick += DebounceTimer_Tick;

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
        TimeoutComboBox.ItemsSource = new[] { "0.5", "1", "2", "5", "10", "30" };
        AppSettings? settings = SettingsService.Load();

        if (settings is not null)
        {
            DataTextBox.Text = settings.CurrentTestData ?? string.Empty;
            ExpressionTextBox.Text = settings.CurrentExpression ?? string.Empty;
            ReplacementTextTextBox.Text = settings.CurrentReplacementText ?? string.Empty;
            ApplyRegexOptionsToCheckBoxes(settings.SelectedOptions);
            ExpandCapturesCheckBox.IsChecked = settings.ExpandCaptures;
            ExpandGroupsCheckBox.IsChecked = settings.ExpandGroups;

            double timeoutSec = settings.TimeoutSeconds is >= 0.1 and <= 300.0
                ? settings.TimeoutSeconds
                : RegexEvaluatorService.DefaultTimeout.TotalSeconds;
            TimeoutComboBox.Text = timeoutSec.ToString(CultureInfo.InvariantCulture);

            if (settings.ExpressionList is { Count: > 0 })
            {
                foreach (string expr in settings.ExpressionList.Where(s => !string.IsNullOrEmpty(s)).Distinct())
                {
                    savedExpressions.Add(expr);
                }
                RefreshSavedExpressionsComboBox();
            }
        }
        else
        {
            TimeoutComboBox.Text = RegexEvaluatorService.DefaultTimeout.TotalSeconds.ToString(CultureInfo.InvariantCulture);
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

        // Debounce evaluation so pasting large text or typing rapidly does not block the UI.
        // C# snippet generation, split parsing, and tree hierarchy construction are offloaded
        // to background worker threads.
        debounceTimer.Stop();
        debounceTimer.Start();
    }

    private void DebounceTimer_Tick(object? sender, EventArgs e)
    {
        debounceTimer.Stop();
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

        _ = SetupAndRunRegexWorkerAsync();
    }

    private void TimeoutComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (stopWork)
        {
            return;
        }

        debounceTimer.Stop();
        debounceTimer.Start();
    }

    private void TimeoutComboBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (stopWork)
        {
            return;
        }

        debounceTimer.Stop();
        debounceTimer.Start();
    }

    public TimeSpan GetConfiguredTimeout()
    {
        string? text = TimeoutComboBox?.Text?.Trim();
        if (string.IsNullOrEmpty(text) && TimeoutComboBox?.SelectedItem is string selected)
        {
            text = selected.Trim();
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) ||
            double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out seconds))
        {
            if (seconds is >= 0.1 and <= 300.0)
            {
                return TimeSpan.FromSeconds(seconds);
            }
        }

        return RegexEvaluatorService.DefaultTimeout;
    }

    private void UpdateTreeExpansionInPlace()
    {
        if (MatchesTreeView.ItemsSource is not IReadOnlyList<MatchTreeNode> roots || roots.Count == 0)
        {
            return;
        }

        bool expandCaptures = ExpandCapturesCheckBox.IsChecked == true;
        bool expandGroups = ExpandGroupsCheckBox.IsChecked == true;

        RegexPresentationService.UpdateExpansion(roots, expandCaptures, expandGroups);
    }

    private void ClearAllButton_Click(object sender, RoutedEventArgs e)
    {
        stopWork = true;
        debounceTimer.Stop();
        CancelPendingEvaluation();

        DataTextBox.Text = string.Empty;
        ExpressionTextBox.Text = string.Empty;
        ReplacementTextTextBox.Text = string.Empty;
        ReplacementResultsTextBox.Text = string.Empty;

        ApplyRegexOptionsToCheckBoxes(RegexOptions.None);
        ExpandCapturesCheckBox.IsChecked = false;
        ExpandGroupsCheckBox.IsChecked = false;
        TimeoutComboBox.Text = RegexEvaluatorService.DefaultTimeout.TotalSeconds.ToString(CultureInfo.InvariantCulture);

        ClearResultsDisplay("Cleared all inputs and options.");
        CSharpSnippetTextBox.Text = string.Empty;

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

        savedExpressions.Remove(expr);
        savedExpressions.Insert(0, expr);
        RefreshSavedExpressionsComboBox();

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
            GetSelectedRegexOptions(),
            GetConfiguredTimeout());
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
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        debounceTimer.Stop();
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
            TimeoutSeconds = GetConfiguredTimeout().TotalSeconds,
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
        bool expandCaptures = ExpandCapturesCheckBox.IsChecked == true;
        bool expandGroups = ExpandGroupsCheckBox.IsChecked == true;
        TimeSpan timeout = GetConfiguredTimeout();

        if (string.IsNullOrEmpty(expression) || string.IsNullOrEmpty(testData))
        {
            ClearResultsDisplay("Enter both a regular expression and test data to evaluate.");
            if (!string.IsNullOrEmpty(expression))
            {
                CSharpSnippetTextBox.Text = CSharpCodeGenerator.GenerateSnippet(expression, replacementText, options, timeout);
            }
            return;
        }

        DisplayResults(RegexPresentationService.CreateLoadingModel());

        try
        {
            RegexDisplayModel displayModel = await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    RegexEvaluationResult evalResult = RegexEvaluatorService.Evaluate(
                        expression, testData, replacementText, options, token, timeout);
                    token.ThrowIfCancellationRequested();
                    return RegexPresentationService.BuildModel(
                        evalResult, expression, replacementText, options, expandCaptures, expandGroups, token, timeout);
                }
                catch (RegexMatchTimeoutException timeoutEx)
                {
                    return RegexPresentationService.CreateTimeoutModel(timeoutEx, expression, replacementText, options);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    return RegexPresentationService.CreateExceptionModel(ex, expression, replacementText, options, timeout);
                }
            }, token);

            if (token.IsCancellationRequested || !ReferenceEquals(regexWorkCts, cts))
            {
                return;
            }

            DisplayResults(displayModel);
        }
        catch (OperationCanceledException)
        {
            // Cancelled by a newer input change.
        }
    }

    private void ClearResultsDisplay(string statusMessage)
    {
        DisplayResults(RegexPresentationService.CreateEmptyModel(statusMessage));
    }

    private void DisplayResults(RegexDisplayModel model)
    {
        MatchesTreeView.ItemsSource = model.TreeNodes;
        SplitResultsListBox.ItemsSource = model.FormattedSplitResults;
        ReplacementResultsTextBox.Text = model.ReplacementResults;
        MatchSummaryTextBlock.Text = model.MatchSummary;
        StatusTextBlock.Text = model.StatusMessage;
        if (!string.IsNullOrEmpty(model.CSharpSnippet))
        {
            CSharpSnippetTextBox.Text = model.CSharpSnippet;
        }
        highlightAdorner?.UpdateHighlights(model.HighlightSpans);
    }

    private void DisplayResults(RegexEvaluationResult result)
    {
        RegexDisplayModel model = RegexPresentationService.BuildModel(
            result,
            ExpressionTextBox.Text,
            ReplacementTextTextBox.Text,
            GetSelectedRegexOptions(),
            ExpandCapturesCheckBox.IsChecked == true,
            ExpandGroupsCheckBox.IsChecked == true,
            matchTimeout: GetConfiguredTimeout());
        DisplayResults(model);
    }

    private void MatchesTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is MatchTreeNode { Span: { } span } &&
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
        RegexDisplayModel model = RegexPresentationService.CreateTimeoutModel(
            ex,
            ExpressionTextBox.Text,
            ReplacementTextTextBox.Text,
            GetSelectedRegexOptions());
        DisplayResults(model);
    }

    private void DisplayException(Exception ex)
    {
        RegexDisplayModel model = RegexPresentationService.CreateExceptionModel(
            ex,
            ExpressionTextBox.Text,
            ReplacementTextTextBox.Text,
            GetSelectedRegexOptions(),
            GetConfiguredTimeout());
        DisplayResults(model);
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
            GetSelectedRegexOptions(),
            GetConfiguredTimeout());
    }
}
