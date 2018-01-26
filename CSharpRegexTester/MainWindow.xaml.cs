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
                var argument = (Tuple<String, String, RegexOptions>)e.Argument;

                var regex = new Regex(argument.Item1, argument.Item3);
                MatchCollection regexMatches = regex.Matches(argument.Item2);

                if (regexMatches == null)
                {
                    return;
                }

                if (regexMatches.Count > 0)
                {
                    TreeViewItem matchesTreeItem = new TreeViewItem { Header = "Matches" };

                    // Matches
                    foreach (Match match in regexMatches)
                    {
                        //TreeViewItem treeItem = new TreeViewItem(string.Format("[{0}] - {1}", i, match.Value));
                        //matchesNode.Nodes.Add(treeItem);

                        //Captures
                        //TreeViewItem captureesNode = new TreeViewItem(string.Format("Captures ({0})", match.Captures.Count));
                        //treeItem.Nodes.Add(captureesNode);
                        for (int i_pos = 0; i_pos < match.Captures.Count; i_pos++)
                        {
                            Capture capture = match.Captures[i_pos];
                            //TreeViewItem captureNode = new TreeViewItem(string.Format("[{0}] - [{1}] ({2} chars)", i_pos, capture.Value, capture.Value.Length));
                            //captureNode.ContextMenuStrip = this.TreeViewItemContextMenuStrip1;
                            //captureesNode.Nodes.Add(captureNode);
                        }

                        //Groups
                        //TreeViewItem groupesNode = new TreeViewItem(string.Format("Groups ({0})", match.Groups.Count));
                        //treeItem.Nodes.Add(groupesNode);
                        for (int i_pos = 0; i_pos < match.Groups.Count; i_pos++)
                        {
                            Group group = match.Groups[i_pos];
                            string groupName = regex.GroupNameFromNumber(i_pos);
                            if (groupName != i_pos.ToString())
                            {
                                groupName = string.Format("<{0}>", groupName);
                            }
                            else
                            {
                                groupName = string.Empty;
                            }

                            //TreeViewItem groupNode = new TreeViewItem(string.Format("[{0}]{1} - [{2}] ({3} chars)", i_pos, groupName, group.Value, group.Value.Length));
                            //groupNode.ContextMenuStrip = this.TreeViewItemContextMenuStrip1;
                            //groupesNode.Nodes.Add(groupNode);
                        }

                        //if (expandcapturesCheckBox.Checked) captureesNode.Expand();
                        //if (expandgroupsCheckBox.Checked) groupesNode.Expand();
                        //treeItem.Expand();
                    }
                    //matchesNode.Expand();

                    //Update Replace View
                    //replaceresultsTextBox.Text = "";
                    //if (ReplaceDelegateTextBox.Text == "")
                    //{
                    //    replaceresultsTextBox.Text = regex.Replace(testData, replacetextTextBox.Text);
                    //}
                }

            }
            catch (Exception ex)
            {
                e.Result = ex;
            }
        }

        private void RegexWorkerWorkComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            TreeViewItem treeViewItem = null;

            try
            {
                treeViewItem = (TreeViewItem)e.Result;
            }
            catch (Exception ex1)
            {
                try
                {
                    ex1 = (Exception)e.Result;

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

            if (treeViewItem != null)
            {
                MatchesTreeView.Items.Clear();
                MatchesTreeView.Items.Add(treeViewItem);
            }
            else
            {
                MatchesTreeView.Items.Clear();
                MatchesTreeView.Items.Add("No Matches");
            }
        }

        private void SetupAndRunRegexWorker()
        {
            if (!IsLoaded)
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

                regexWorker.RunWorkerAsync(new Tuple<String, String, RegexOptions>(ExpressionTextBox.Text, DataTextBox.Text, options));
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

        private void ExpressionTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (IsLoaded && ExpressionTextBox.Text == "Expression")
            {
                ExpressionTextBox.Text = "";
            }
        }

        private void ExpressionTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (regexWorker.IsBusy)
            {
                regexWorker.CancelAsync();
            }

            SetupAndRunRegexWorker();
        }
    }
}
