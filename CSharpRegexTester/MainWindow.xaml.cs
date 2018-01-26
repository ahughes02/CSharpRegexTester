using System;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace CSharpRegexTester
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool stopWork = false;
        private readonly BackgroundWorker regexWorker = new BackgroundWorker();

        public MainWindow()
        {
            InitializeComponent();
            regexWorker.DoWork += RegexWorkerDoWork;
            regexWorker.RunWorkerCompleted += RegexWorkerWorkComplete;
        }

        private void RegexWorkerDoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                var argument = (Tuple<String, String, String, RegexOptions>)e.Argument;

                string replaceWith = String.IsNullOrEmpty(argument.Item3) ? "" : argument.Item3;
                string replacementResults = argument.Item2;

                var regex = new Regex(argument.Item1, argument.Item4);
                MatchCollection matches = regex.Matches(argument.Item2);

                if (matches.Count > 0)
                {
                    // Update Replace View
                    replacementResults = regex.Replace(replacementResults, replaceWith);
                    e.Result = new Tuple<Regex, MatchCollection, String>(regex, matches, replacementResults);
                }
                else
                {
                    e.Result = null;
                }

            }
            catch (Exception ex)
            {
                e.Result = ex;
            }
        }

        private void RegexWorkerWorkComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            Tuple<Regex, MatchCollection, String> result = null;

            if (e.Result != null)
            {
                try
                {
                    result = (Tuple<Regex, MatchCollection, String>)e.Result;
                }
                catch (Exception ex1)
                {
                    try
                    {
                        ex1 = (Exception)e.Result;

                        MatchesTreeView.Items.Clear();

                        var treeItem = new TreeViewItem { Header = "Exception" };
                        treeItem.Items.Add(new TreeViewItem { Header = String.Format("Message: {0}", ex1.Message) });
                        treeItem.Items.Add(new TreeViewItem { Header = String.Format("Source: {0}", ex1.Source) });
                        treeItem.IsExpanded = true;

                        MatchesTreeView.Items.Add(treeItem);

                        return;
                    }
                    catch (Exception ex2)
                    {
                        MatchesTreeView.Items.Clear();

                        var treeItem = new TreeViewItem { Header = "Exception" };
                        treeItem.Items.Add(new TreeViewItem { Header = String.Format("Message: {0}", ex2.Message) });
                        treeItem.Items.Add(new TreeViewItem { Header = String.Format("Source: {0}", ex2.Source) });
                        treeItem.IsExpanded = true;

                        MatchesTreeView.Items.Add(treeItem);

                        return;
                    }
                }

                if (result != null)
                {

                    Regex regex = result.Item1;
                    MatchCollection matches = result.Item2;

                    if (matches == null)
                    {
                        return;
                    }

                    TreeViewItem matchesTreeItem = null;

                    if (matches.Count > 0)
                    {
                        matchesTreeItem = new TreeViewItem { Header = "Matches" };

                        // Matches
                        for (int i = 0; i < matches.Count; i++)
                        {
                            Match match = matches[i];

                            TreeViewItem matchTreeItem = new TreeViewItem { Header = String.Format("[{0}] - {1}", i, match.Value) };
                            matchesTreeItem.Items.Add(matchTreeItem);

                            // Captures
                            TreeViewItem capturesTreeItem = new TreeViewItem { Header = String.Format("Captures ({0})", match.Captures.Count) };
                            matchTreeItem.Items.Add(capturesTreeItem);
                            for (int j = 0; j < match.Captures.Count; j++)
                            {
                                Capture capture = match.Captures[j];
                                TreeViewItem captureTreeItem = new TreeViewItem { Header = String.Format("[{0}] - [{1}] ({2} chars)", j, capture.Value, capture.Value.Length) };
                                capturesTreeItem.Items.Add(captureTreeItem);
                            }

                            // Groups
                            TreeViewItem groupsTreeItem = new TreeViewItem { Header = String.Format("Groups ({0})", match.Groups.Count) };
                            matchTreeItem.Items.Add(groupsTreeItem);
                            for (int j = 0; j < match.Groups.Count; j++)
                            {
                                Group group = match.Groups[j];
                                var groupName = regex.GroupNameFromNumber(j);

                                if (groupName != j.ToString())
                                {
                                    groupName = String.Format("<{0}>", groupName);
                                }
                                else
                                {
                                    groupName = String.Empty;
                                }

                                TreeViewItem groupTreeItem = new TreeViewItem { Header = String.Format("[{0}]{1} - [{2}] ({3} chars)", j, groupName, group.Value, group.Value.Length) };
                                groupsTreeItem.Items.Add(groupTreeItem);
                            }

                            if (ExpandCapturesCheckBox.IsChecked.Value)
                            {
                                capturesTreeItem.IsExpanded = true;
                            }
                            if (ExpandGroupsCheckBox.IsChecked.Value)
                            {
                                groupsTreeItem.IsExpanded = true;
                            }

                            matchTreeItem.IsExpanded = true;
                        }

                        matchesTreeItem.IsExpanded = true;

                        MatchesTreeView.Items.Clear();
                        MatchesTreeView.Items.Add(matchesTreeItem);
                        ReplacementResultsTextBox.Text = result.Item3;
                    }
                    else
                    {
                        MatchesTreeView.Items.Clear();
                        MatchesTreeView.Items.Add("No Matches");
                        ReplacementResultsTextBox.Text = "";
                    }
                }
            }
            else
            {
                MatchesTreeView.Items.Clear();
                MatchesTreeView.Items.Add("No Matches");
                ReplacementResultsTextBox.Text = "";
            }
        }

        private void SetupAndRunRegexWorker()
        {
            if (!IsLoaded || stopWork)
            {
                return;
            }

            RegexOptions options = RegexOptions.None;
            if (IgnoreCaseCheckBox.IsChecked.Value)
                options = options | RegexOptions.IgnoreCase;
            if (ExplicitCaptureCheckBox.IsChecked.Value)
                options = options | RegexOptions.ExplicitCapture;
            if (CompiledCheckBox.IsChecked.Value)
                options = options | RegexOptions.Compiled;
            if (CultureInvariantCheckBox.IsChecked.Value)
                options = options | RegexOptions.CultureInvariant;
            if (ECMAScriptCheckBox.IsChecked.Value)
                options = options | RegexOptions.ECMAScript;
            if (IgnorePatternWhitespace.IsChecked.Value)
                options = options | RegexOptions.IgnorePatternWhitespace;
            if (MultilineCheckBox.IsChecked.Value)
                options = options | RegexOptions.Multiline;
            if (RightToLeftCheckBox.IsChecked.Value)
                options = options | RegexOptions.RightToLeft;
            if (SinglelineCheckBox.IsChecked.Value)
                options = options | RegexOptions.Singleline;

            try
            {
                if (String.IsNullOrWhiteSpace(ExpressionTextBox.Text) || String.IsNullOrWhiteSpace(DataTextBox.Text))
                {
                    return;
                }

                MatchesTreeView.Items.Clear();
                MatchesTreeView.Items.Add("Working...");

                regexWorker.RunWorkerAsync(new Tuple<String, String, String, RegexOptions>(ExpressionTextBox.Text, DataTextBox.Text, ReplacementTextTextBox.Text, options));
            }
            catch (Exception e)
            {
                MatchesTreeView.Items.Clear();

                var treeItem = new TreeViewItem { Header = "Exception" };
                treeItem.Items.Add(new TreeViewItem { Header = String.Format("Message: {0}", e.Message) });
                treeItem.Items.Add(new TreeViewItem { Header = String.Format("Source: {0}", e.Source) });
                treeItem.IsExpanded = true;

                MatchesTreeView.Items.Add(treeItem);
            }
        }

        private void ExpressionTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (IsLoaded && ExpressionTextBox.Text == "Expression")
            {
                ExpressionTextBox.Text = "";
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (regexWorker.IsBusy)
            {
                regexWorker.CancelAsync();
            }

            SetupAndRunRegexWorker();
        }

        private void Checkbox_CheckChanged(object sender, EventArgs e)
        {
            if (regexWorker.IsBusy)
            {
                regexWorker.CancelAsync();
            }

            SetupAndRunRegexWorker();
        }

        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            stopWork = true;
            DataTextBox.Text = "";
            ExpressionTextBox.Text = "";
            ReplacementTextTextBox.Text = "";
            ReplacementResultsTextBox.Text = "";
            IgnoreCaseCheckBox.IsChecked = false;
            MatchesTreeView.Items.Clear();
            stopWork = false;
        }
    }
}
