using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace CSharpRegexTester;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private bool stopWork;
    private CancellationTokenSource? regexWorkCts;

    private sealed record RegexWorkResult(Regex Regex, MatchCollection Matches, string ReplacementResults);

    public MainWindow()
    {
        InitializeComponent();

        var configFile = RegexTesterConfigurationFile.Load();

        if (configFile is not null)
        {
            DataTextBox.Text = configFile.CurrentTestData;
            ExpressionTextBox.Text = configFile.CurrentExpression;
            ReplacementTextTextBox.Text = configFile.CurrentReplacementText;
        }

        Loaded += (_, _) => _ = SetupAndRunRegexWorkerAsync();
    }

    /// <summary>
    /// Text Changed Handler for ExpressionTextBox, DataTextBox and, ReplacementTextTextBox
    /// </summary>
    private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _ = SetupAndRunRegexWorkerAsync();
    }

    /// <summary>
    /// Check Changed Handler for options checkboxes
    /// </summary>
    private void Checkbox_CheckChanged(object sender, RoutedEventArgs e)
    {
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
        IgnoreCaseCheckBox.IsChecked = false;
        MatchesTreeView.Items.Clear();
        stopWork = false;
    }

    private void RegexCharacterButton_Click(object sender, RoutedEventArgs e)
    {
        if (e.Source is Button { Content: string content })
        {
            ExpressionTextBox.Text += content;
        }
    }

    private void SaveAndExitButton_Click(object sender, RoutedEventArgs e)
    {
        var configFile = new RegexTesterConfigurationFile
        {
            CurrentExpression = ExpressionTextBox.Text,
            CurrentReplacementText = ReplacementTextTextBox.Text,
            CurrentTestData = DataTextBox.Text
        };
        configFile.Save();

        Application.Current.Shutdown();
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

        return options;
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

        if (string.IsNullOrWhiteSpace(expression) || string.IsNullOrWhiteSpace(testData))
        {
            return;
        }

        MatchesTreeView.Items.Clear();
        MatchesTreeView.Items.Add("Working...");

        try
        {
            RegexWorkResult? result = await Task.Run(
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
            // Cancelled by a newer request.
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                DisplayException(ex);
            }
        }
    }

    private static RegexWorkResult? EvaluateRegex(
        string pattern,
        string input,
        string replacement,
        RegexOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string replaceWith = replacement ?? string.Empty;
        var regex = new Regex(pattern, options);
        MatchCollection matches = regex.Matches(input);

        // Force evaluation of matches on the background thread while checking for cancellation.
        if (matches.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Update Replace View
            string replacementResults = regex.Replace(input, replaceWith);
            return new RegexWorkResult(regex, matches, replacementResults);
        }

        return null;
    }

    private void DisplayResults(RegexWorkResult? result)
    {
        if (result is { Matches.Count: > 0 } validResult)
        {
            Regex regex = validResult.Regex;
            MatchCollection matches = validResult.Matches;

            var matchesTreeItem = new TreeViewItem { Header = "Matches" };

            // Matches
            for (int i = 0; i < matches.Count; i++)
            {
                Match match = matches[i];

                var matchTreeItem = new TreeViewItem { Header = $"[{i}] - {match.Value}" };
                matchesTreeItem.Items.Add(matchTreeItem);

                // Captures
                var capturesTreeItem = new TreeViewItem { Header = $"Captures ({match.Captures.Count})" };
                matchTreeItem.Items.Add(capturesTreeItem);
                for (int j = 0; j < match.Captures.Count; j++)
                {
                    Capture capture = match.Captures[j];
                    var captureTreeItem = new TreeViewItem { Header = $"[{j}] - [{capture.Value}] ({capture.Value.Length} chars)" };
                    capturesTreeItem.Items.Add(captureTreeItem);
                }

                // Groups
                var groupsTreeItem = new TreeViewItem { Header = $"Groups ({match.Groups.Count})" };
                matchTreeItem.Items.Add(groupsTreeItem);
                for (int j = 0; j < match.Groups.Count; j++)
                {
                    Group group = match.Groups[j];
                    string rawGroupName = regex.GroupNameFromNumber(j);
                    string groupName = rawGroupName != j.ToString()
                        ? $"<{rawGroupName}>"
                        : string.Empty;

                    var groupTreeItem = new TreeViewItem { Header = $"[{j}]{groupName} - [{group.Value}] ({group.Value.Length} chars)" };
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
            ReplacementResultsTextBox.Text = validResult.ReplacementResults;
        }
        else
        {
            MatchesTreeView.Items.Clear();
            MatchesTreeView.Items.Add("No Matches");
            ReplacementResultsTextBox.Text = string.Empty;
        }
    }

    private void DisplayException(Exception ex)
    {
        MatchesTreeView.Items.Clear();

        var treeItem = new TreeViewItem { Header = "Exception" };
        treeItem.Items.Add(new TreeViewItem { Header = $"Message: {ex.Message}" });
        treeItem.Items.Add(new TreeViewItem { Header = $"Source: {ex.Source}" });
        treeItem.IsExpanded = true;

        MatchesTreeView.Items.Add(treeItem);
    }
}
