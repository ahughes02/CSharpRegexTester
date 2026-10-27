using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CSharpRegexTester.Controls;
using CSharpRegexTester.Models;
using CSharpRegexTester.Services;

namespace CSharpRegexTester.Tests;

public class MainWindowAndAdornerTests
{
    private static void RunOnStaThread(Action action)
    {
        Exception? capturedException = null;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            try
            {
                action();
            }
            catch (Exception ex)
            {
                capturedException = ex;
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (capturedException is not null)
        {
            throw new AggregateException(capturedException);
        }
    }

    private static void WaitWithDispatcher(Task task)
    {
        var frame = new DispatcherFrame();
        _ = task.ContinueWith(_ => frame.Continue = false, TaskScheduler.Default);
        Dispatcher.PushFrame(frame);
        task.GetAwaiter().GetResult();
    }

    [Fact]
    public void RegexMatchHighlightAdorner_UpdatesAndRendersSingleAndMultiLineSpans()
    {
        RunOnStaThread(() =>
        {
            var textBox = new TextBox
            {
                Width = 300,
                Height = 120,
                Text = "First line\r\nSecond line with text"
            };
            textBox.Measure(new Size(300, 120));
            textBox.Arrange(new Rect(0, 0, 300, 120));
            textBox.UpdateLayout();

            var adorner = new RegexMatchHighlightAdorner(textBox);
            adorner.Measure(new Size(300, 120));
            adorner.Arrange(new Rect(0, 0, 300, 120));

            // Single-line and multi-line spans + out-of-range span
            var spans = new List<TextSpan>
            {
                new(0, 5),
                new(6, 15),
                new(-1, 3),
                new(0, 500)
            };

            adorner.UpdateHighlights(spans, new TextSpan(0, 5));
            adorner.SetSelectedSpan(new TextSpan(6, 15));

            var drawingGroup = new DrawingGroup();
            using (DrawingContext dc = drawingGroup.Open())
            {
                MethodInfo? onRender = typeof(RegexMatchHighlightAdorner)
                    .GetMethod("OnRender", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(onRender);
                onRender.Invoke(adorner, [dc]);
            }

            // Also test empty render branch
            adorner.UpdateHighlights([]);
            using (DrawingContext dc = drawingGroup.Open())
            {
                MethodInfo? onRender = typeof(RegexMatchHighlightAdorner)
                    .GetMethod("OnRender", BindingFlags.Instance | BindingFlags.NonPublic);
                onRender!.Invoke(adorner, [dc]);
            }
        });
    }

    [Fact]
    public void MainWindow_Workflows_Options_Buttons_Results_And_Persistence_WorkCorrectly()
    {
        RunOnStaThread(() =>
        {
            // Seed configuration before creating MainWindow
            SettingsService.Save(new AppSettings
            {
                CurrentExpression = @"(?<word>\w+)",
                CurrentTestData = "Hello 123",
                CurrentReplacementText = "[$1]",
                SelectedOptions = RegexOptions.IgnoreCase | RegexOptions.Multiline,
                ExpandCaptures = true,
                ExpandGroups = true,
                ExpressionList = [@"(?<word>\w+)", @"\d+"]
            });

            var window = new MainWindow();

            // Invoke MainWindow_Loaded
            InvokePrivate(window, "MainWindow_Loaded", window, new RoutedEventArgs());

            Assert.Equal(@"(?<word>\w+)", window.ExpressionTextBox.Text);
            Assert.Equal("Hello 123", window.DataTextBox.Text);
            Assert.True(window.IgnoreCaseCheckBox.IsChecked);
            Assert.True(window.MultilineCheckBox.IsChecked);

            // Test DisplayResults with matches and non-matching optional groups
            RegexEvaluationResult matchResult = RegexEvaluatorService.Evaluate(
                @"(?<word>[A-Za-z]+)|(?<num>\d+)",
                "Hello 123",
                "[$0]",
                RegexOptions.None);
            InvokePrivate(window, "DisplayResults", matchResult);

            Assert.Single(window.MatchesTreeView.Items);
            var rootItem = Assert.IsType<TreeViewItem>(window.MatchesTreeView.Items[0]);
            Assert.Equal("Matches (2)", rootItem.Header);
            Assert.Equal("[Hello] [123]", window.ReplacementResultsTextBox.Text);

            // Select a match TreeViewItem to trigger MatchesTreeView_SelectedItemChanged
            var firstMatchItem = Assert.IsType<TreeViewItem>(rootItem.Items[0]);
            InvokePrivate(
                window,
                "MatchesTreeView_SelectedItemChanged",
                window.MatchesTreeView,
                new RoutedPropertyChangedEventArgs<object>(null!, firstMatchItem));
            Assert.Contains("Selected span:", window.StatusTextBlock.Text);

            // Select an item without a TextSpan Tag
            InvokePrivate(
                window,
                "MatchesTreeView_SelectedItemChanged",
                window.MatchesTreeView,
                new RoutedPropertyChangedEventArgs<object>(firstMatchItem, rootItem));

            // Test DisplayResults with 1 match (singular "1 match" branch) and ExpandCaptures/ExpandGroups = false
            window.ExpandCapturesCheckBox.IsChecked = false;
            window.ExpandGroupsCheckBox.IsChecked = false;
            RegexEvaluationResult singleMatchResult = RegexEvaluatorService.Evaluate(
                @"Hello",
                "Hello 123",
                "Hi",
                RegexOptions.None);
            InvokePrivate(window, "DisplayResults", singleMatchResult);
            Assert.Contains("1 match", window.MatchSummaryTextBlock.Text);

            // Test in-place tree expansion toggle via ExpandCapturesCheckBox and ExpandGroupsCheckBox
            window.ExpandCapturesCheckBox.IsChecked = true;
            InvokePrivate(window, "Checkbox_CheckChanged", window.ExpandCapturesCheckBox, new RoutedEventArgs());
            window.ExpandGroupsCheckBox.IsChecked = true;
            InvokePrivate(window, "Checkbox_CheckChanged", window.ExpandGroupsCheckBox, new RoutedEventArgs());

            // Test DisplayResults with 0 matches
            RegexEvaluationResult emptyResult = RegexEvaluatorService.Evaluate(
                @"999",
                "Hello 123",
                "",
                RegexOptions.None);
            InvokePrivate(window, "DisplayResults", emptyResult);
            Assert.Equal("No Matches", window.MatchesTreeView.Items[0]);

            // Test Checkbox_CheckChanged when stopWork is false
            InvokePrivate(window, "Checkbox_CheckChanged", window.IgnoreCaseCheckBox, new RoutedEventArgs());

            // Test RegexCharacterButton_Click edge cases (non-button source, button with Content only, empty button)
            InvokePrivate(window, "RegexCharacterButton_Click", window, new RoutedEventArgs(Button.ClickEvent, window));
            var contentOnlyButton = new Button { Content = @"\w" };
            InvokePrivate(window, "RegexCharacterButton_Click", contentOnlyButton, new RoutedEventArgs(Button.ClickEvent, contentOnlyButton));
            var emptyButton = new Button();
            InvokePrivate(window, "RegexCharacterButton_Click", emptyButton, new RoutedEventArgs(Button.ClickEvent, emptyButton));

            // Test DisplayTimeoutException & DisplayException
            InvokePrivate(
                window,
                "DisplayTimeoutException",
                new RegexMatchTimeoutException("Hello", @"\w+", TimeSpan.FromSeconds(2)));
            Assert.Equal("Timed out", window.MatchSummaryTextBlock.Text);

            InvokePrivate(
                window,
                "DisplayException",
                new ArgumentException("Invalid pattern"));
            Assert.Equal("Invalid Pattern", window.MatchSummaryTextBlock.Text);

            // Test Quick-Insert Button Click
            window.ExpressionTextBox.Text = "abc";
            window.ExpressionTextBox.SelectionStart = 0;
            window.ExpressionTextBox.SelectionLength = 3;
            InvokePrivate(
                window,
                "RegexCharacterButton_Click",
                window.GroupButton,
                new RoutedEventArgs(Button.ClickEvent, window.GroupButton));
            Assert.Equal("(abc)", window.ExpressionTextBox.Text);

            // Test Save & Remove Saved Expression
            InvokePrivate(window, "SaveExpressionButton_Click", window.SaveExpressionButton, new RoutedEventArgs());
            Assert.Equal("(abc)", window.SavedExpressionsComboBox.SelectedItem);

            InvokePrivate(
                window,
                "SavedExpressionsComboBox_SelectionChanged",
                window.SavedExpressionsComboBox,
                new SelectionChangedEventArgs(ComboBox.SelectionChangedEvent, Array.Empty<object>(), Array.Empty<object>()));

            InvokePrivate(window, "RemoveSavedExpressionButton_Click", window.RemoveSavedExpressionButton, new RoutedEventArgs());

            // Test CopyCSharpButton_Click
            InvokePrivate(window, "CopyCSharpButton_Click", window.CopyCSharpButton, new RoutedEventArgs());
            Assert.Contains("[GeneratedRegex", window.CSharpSnippetTextBox.Text);

            // Test ClearAllButton_Click
            InvokePrivate(window, "ClearAllButton_Click", window.ClearAllButton, new RoutedEventArgs());
            Assert.Equal(string.Empty, window.ExpressionTextBox.Text);
            Assert.False(window.IgnoreCaseCheckBox.IsChecked);

            // Test SaveExpressionButton_Click when expression is empty
            InvokePrivate(window, "SaveExpressionButton_Click", window.SaveExpressionButton, new RoutedEventArgs());
            Assert.Equal("Enter a regular expression before saving.", window.StatusTextBlock.Text);

            // Test async evaluation pipeline (valid, invalid, and empty inputs)
            window.ExpressionTextBox.Text = @"(?<word>\w+)";
            window.DataTextBox.Text = "Async Test 42";
            WaitWithDispatcher((Task)InvokePrivate(window, "SetupAndRunRegexWorkerAsync")!);

            window.ExpressionTextBox.Text = "[invalid";
            WaitWithDispatcher((Task)InvokePrivate(window, "SetupAndRunRegexWorkerAsync")!);
            Assert.Equal("Invalid Pattern", window.MatchSummaryTextBlock.Text);

            window.ExpressionTextBox.Text = string.Empty;
            WaitWithDispatcher((Task)InvokePrivate(window, "SetupAndRunRegexWorkerAsync")!);

            // Test Window_Closing saves configuration
            InvokePrivate(window, "Window_Closing", window, new System.ComponentModel.CancelEventArgs());
        });
    }

    private static object? InvokePrivate(object target, string methodName, params object[] args)
    {
        MethodInfo? method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method.Invoke(target, args);
    }
}
