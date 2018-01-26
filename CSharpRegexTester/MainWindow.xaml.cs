using System;
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
        public MainWindow()
        {
            InitializeComponent();
        }

        private void UpdateDisplay()
        {
            if(!IsLoaded)
            {
                return;
            }

            RegexOptions options = RegexOptions.None;
            #region Assemble Options
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

            #endregion
            Regex regEx = null;

            try
            {
                regEx = new Regex(ExpressionTextBox.Text, options);
            }
            catch (Exception e)
            {
                if (MatchesTreeView.Items != null)
                {
                    MatchesTreeView.Items.Clear();
                }

                var treeItem = new TreeViewItem { Header = "Exception" };
                treeItem.Items.Add(new TreeViewItem { Header = string.Format("Message: {0}", e.Message) });
                treeItem.Items.Add(new TreeViewItem { Header = string.Format("Source: {0}", e.Source) });
                treeItem.IsExpanded = true;

                MatchesTreeView.Items.Add(treeItem);
            }

            /*
            if (regEx == null) return;
            string sData = dataTextBox.Text;
            if (regEx.IsMatch(sData) && sData.Length > 0)
            {
                //Update Match View
                MatchesTreeView.Items.Clear();
                TreeViewItem matchesNode = new TreeViewItem("Matches");
                MatchesTreeView.Items.Add(matchesNode);
                MatchCollection Matches = regEx.Matches(sData);
                //Matches
                for (int i = 0; i < Matches.Count; i++)
                {
                    Match match = Matches[i];
                    TreeViewItem treeItem = new TreeViewItem(string.Format("[{0}] - {1}", i, match.Value));
                    matchesNode.Nodes.Add(treeItem);

                    //Captures
                    TreeViewItem captureesNode = new TreeViewItem(string.Format("Captures ({0})", match.Captures.Count));
                    treeItem.Nodes.Add(captureesNode);
                    for (int i_pos = 0; i_pos < match.Captures.Count; i_pos++)
                    {
                        Capture capture = match.Captures[i_pos];
                        TreeViewItem captureNode = new TreeViewItem(string.Format("[{0}] - [{1}] ({2} chars)", i_pos, capture.Value, capture.Value.Length));
                        captureNode.ContextMenuStrip = this.TreeViewItemContextMenuStrip1;
                        captureesNode.Nodes.Add(captureNode);
                    }

                    //Groups
                    TreeViewItem groupesNode = new TreeViewItem(string.Format("Groups ({0})", match.Groups.Count));
                    treeItem.Nodes.Add(groupesNode);
                    for (int i_pos = 0; i_pos < match.Groups.Count; i_pos++)
                    {
                        Group group = match.Groups[i_pos];
                        string groupName = regEx.GroupNameFromNumber(i_pos);
                        if (groupName != i_pos.ToString())
                            groupName = string.Format("<{0}>", groupName);
                        else
                            groupName = string.Empty;

                        TreeViewItem groupNode = new TreeViewItem(string.Format("[{0}]{1} - [{2}] ({3} chars)", i_pos, groupName, group.Value, group.Value.Length));
                        groupNode.ContextMenuStrip = this.TreeViewItemContextMenuStrip1;
                        groupesNode.Nodes.Add(groupNode);
                    }

                    if (expandcapturesCheckBox.Checked) captureesNode.Expand();
                    if (expandgroupsCheckBox.Checked) groupesNode.Expand();
                    treeItem.Expand();
                }
                matchesNode.Expand();

                //Update Replace View
                replaceresultsTextBox.Text = "";
                if (ReplaceDelegateTextBox.Text == "")
                {
                    replaceresultsTextBox.Text = regEx.Replace(sData, replacetextTextBox.Text);
                }
            }
            else
            {
                MatchesTreeView.Items.Clear();
                MatchesTreeView.Items.Add("No Matches");
            }
            */
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
            UpdateDisplay();
        }
    }
}
