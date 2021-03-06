﻿using System;
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

            var configFile = RegexTesterConfigurationFile.Load();

            if (configFile != null)
            {
                DataTextBox.Text = configFile.CurrentTestData;
                ExpressionTextBox.Text = configFile.CurrentExpression;
                ReplacementTextTextBox.Text = configFile.CurrentReplacementText;
            }

            SetupAndRunRegexWorker();
        }

        /// <summary>
        /// Text Changed Handler for ExpressionTextBox, DataTextBox and, ReplacementTextTextBox
        /// </summary>
        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (regexWorker.IsBusy)
            {
                regexWorker.CancelAsync();
            }

            SetupAndRunRegexWorker();
        }

        /// <summary>
        /// Check Changed Handler for options checkboxes
        /// </summary>
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

        private void RegexCharacterButton_Click(object sender, RoutedEventArgs e)
        {
            var source = (Button)e.Source;
            string content = (string)source.Content;

            ExpressionTextBox.Text += content;
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

        private void SetupAndRunRegexWorker()
        {
            if (!IsLoaded || stopWork)
            {
                return;
            }

            RegexOptions options = RegexOptions.None;
            if (IgnoreCaseCheckBox.IsChecked.Value)
            {
                options |= RegexOptions.IgnoreCase;
            }

            if (ExplicitCaptureCheckBox.IsChecked.Value)
            {
                options |= RegexOptions.ExplicitCapture;
            }

            if (CompiledCheckBox.IsChecked.Value)
            {
                options |= RegexOptions.Compiled;
            }

            if (CultureInvariantCheckBox.IsChecked.Value)
            {
                options |= RegexOptions.CultureInvariant;
            }

            if (ECMAScriptCheckBox.IsChecked.Value)
            {
                options |= RegexOptions.ECMAScript;
            }

            if (IgnorePatternWhitespace.IsChecked.Value)
            {
                options |= RegexOptions.IgnorePatternWhitespace;
            }

            if (MultilineCheckBox.IsChecked.Value)
            {
                options |= RegexOptions.Multiline;
            }

            if (RightToLeftCheckBox.IsChecked.Value)
            {
                options |= RegexOptions.RightToLeft;
            }

            if (SinglelineCheckBox.IsChecked.Value)
            {
                options |= RegexOptions.Singleline;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(ExpressionTextBox.Text) || string.IsNullOrWhiteSpace(DataTextBox.Text))
                {
                    return;
                }

                MatchesTreeView.Items.Clear();
                MatchesTreeView.Items.Add("Working...");

                regexWorker.RunWorkerAsync(new Tuple<string, string, string, RegexOptions>(ExpressionTextBox.Text, DataTextBox.Text, ReplacementTextTextBox.Text, options));
            }
            catch (Exception e)
            {
                MatchesTreeView.Items.Clear();

                var treeItem = new TreeViewItem { Header = "Exception" };
                treeItem.Items.Add(new TreeViewItem { Header = string.Format("Message: {0}", e.Message) });
                treeItem.Items.Add(new TreeViewItem { Header = string.Format("Source: {0}", e.Source) });
                treeItem.IsExpanded = true;

                MatchesTreeView.Items.Add(treeItem);
            }
        }

        private void RegexWorkerDoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                var argument = (Tuple<string, string, string, RegexOptions>)e.Argument;

                string replaceWith = string.IsNullOrEmpty(argument.Item3) ? "" : argument.Item3;
                string replacementResults = argument.Item2;

                var regex = new Regex(argument.Item1, argument.Item4);
                MatchCollection matches = regex.Matches(argument.Item2);

                if (matches.Count > 0)
                {
                    // Update Replace View
                    replacementResults = regex.Replace(replacementResults, replaceWith);
                    e.Result = new Tuple<Regex, MatchCollection, string>(regex, matches, replacementResults);
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
            if (e.Result != null)
            {
                Tuple<Regex, MatchCollection, string> result;
                try
                {
                    result = (Tuple<Regex, MatchCollection, string>)e.Result;
                }
                catch (Exception)
                {
                    try
                    {
                        // TODO: Is this right?
                        var ex1 = (Exception)e.Result;

                        MatchesTreeView.Items.Clear();

                        var treeItem = new TreeViewItem { Header = "Exception" };
                        treeItem.Items.Add(new TreeViewItem { Header = string.Format("Message: {0}", ex1.Message) });
                        treeItem.Items.Add(new TreeViewItem { Header = string.Format("Source: {0}", ex1.Source) });
                        treeItem.IsExpanded = true;

                        MatchesTreeView.Items.Add(treeItem);

                        return;
                    }
                    catch (Exception ex2)
                    {
                        MatchesTreeView.Items.Clear();

                        var treeItem = new TreeViewItem { Header = "Exception" };
                        treeItem.Items.Add(new TreeViewItem { Header = string.Format("Message: {0}", ex2.Message) });
                        treeItem.Items.Add(new TreeViewItem { Header = string.Format("Source: {0}", ex2.Source) });
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

                    if (matches.Count > 0)
                    {
                        var matchesTreeItem = new TreeViewItem { Header = "Matches" };

                        // Matches
                        for (int i = 0; i < matches.Count; i++)
                        {
                            Match match = matches[i];

                            var matchTreeItem = new TreeViewItem { Header = string.Format("[{0}] - {1}", i, match.Value) };
                            matchesTreeItem.Items.Add(matchTreeItem);

                            // Captures
                            var capturesTreeItem = new TreeViewItem { Header = string.Format("Captures ({0})", match.Captures.Count) };
                            matchTreeItem.Items.Add(capturesTreeItem);
                            for (int j = 0; j < match.Captures.Count; j++)
                            {
                                Capture capture = match.Captures[j];
                                var captureTreeItem = new TreeViewItem { Header = string.Format("[{0}] - [{1}] ({2} chars)", j, capture.Value, capture.Value.Length) };
                                capturesTreeItem.Items.Add(captureTreeItem);
                            }

                            // Groups
                            var groupsTreeItem = new TreeViewItem { Header = string.Format("Groups ({0})", match.Groups.Count) };
                            matchTreeItem.Items.Add(groupsTreeItem);
                            for (int j = 0; j < match.Groups.Count; j++)
                            {
                                Group group = match.Groups[j];
                                string groupName = regex.GroupNameFromNumber(j);

                                if (groupName != j.ToString())
                                {
                                    groupName = string.Format("<{0}>", groupName);
                                }
                                else
                                {
                                    groupName = string.Empty;
                                }

                                var groupTreeItem = new TreeViewItem { Header = string.Format("[{0}]{1} - [{2}] ({3} chars)", j, groupName, group.Value, group.Value.Length) };
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
    }
}
