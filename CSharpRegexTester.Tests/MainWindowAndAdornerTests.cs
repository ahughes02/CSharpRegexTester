using System.IO;
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

    private static void WithIsolatedWindow(AppSettings? seedSettings, Action<MainWindow, string> testAction)
    {
        RunOnStaThread(() =>
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "CSharpRegexTesterTests", Guid.NewGuid().ToString("N"));
            string configPath = Path.Combine(tempDir, "test_settings.cfg");
            Directory.CreateDirectory(tempDir);

            try
            {
                if (seedSettings is not null)
                {
                    SettingsService.Save(seedSettings, configPath);
                }

                var window = new MainWindow(configPath);
                window.Measure(new Size(1120, 720));
                window.Arrange(new Rect(0, 0, 1120, 720));
                window.UpdateLayout();

                InvokePrivate(window, "MainWindow_Loaded", window, new RoutedEventArgs());
                testAction(window, configPath);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
        });
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
            textBox.ApplyTemplate();
            textBox.Measure(new Size(300, 120));
            textBox.Arrange(new Rect(0, 0, 300, 120));
            textBox.UpdateLayout();

            var adorner = new RegexMatchHighlightAdorner(
                textBox,
                (charIndex, trailing) =>
                {
                    if (charIndex == 11)
                    {
                        return Rect.Empty;
                    }

                    int line = charIndex < 12 ? 0 : 1;
                    int col = line == 0 ? charIndex : charIndex - 12;
                    double x = col * 8 + (trailing ? 8 : 0);
                    double y = line * 20;
                    return new Rect(x, y, 1, 16);
                });
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
                InvokeOnRender(adorner, dc);
            }
            Assert.NotEmpty(drawingGroup.Children);

            // Empty render branch and small bounds early return
            adorner.UpdateHighlights([]);
            using (DrawingContext dc = drawingGroup.Open())
            {
                InvokeOnRender(adorner, dc);
            }

            textBox.Measure(new Size(2, 2));
            textBox.Arrange(new Rect(0, 0, 2, 2));
            using (DrawingContext dc = drawingGroup.Open())
            {
                InvokeOnRender(adorner, dc);
            }
        });
    }

    [Fact]
    public void RegexMatchHighlightAdorner_RendersMatchesBeyondMaxRenderMatchesWhenScrolledDown()
    {
        RunOnStaThread(() =>
        {
            // Build 1200 lines ("m\r\n" per line) so match indices 1050..1055 are far past MaxRenderMatches (1000)
            const int lineCount = 1200;
            string lineText = "m\r\n";
            string fullText = string.Concat(Enumerable.Repeat(lineText, lineCount));

            var textBox = new TextBox
            {
                Width = 300,
                Height = 120,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Text = fullText
            };
            textBox.ApplyTemplate();
            textBox.Measure(new Size(300, 120));
            textBox.Arrange(new Rect(0, 0, 300, 120));
            textBox.UpdateLayout();

            // Scroll near line 1100
            textBox.ScrollToLine(1100);
            textBox.UpdateLayout();

            var spans = new List<TextSpan>(lineCount);
            for (int i = 0; i < lineCount; i++)
            {
                spans.Add(new TextSpan(i * lineText.Length, 1));
            }

            var adorner = new RegexMatchHighlightAdorner(
                textBox,
                (charIndex, trailing) =>
                {
                    int line = charIndex / lineText.Length;
                    int firstVisible = Math.Max(0, textBox.GetFirstVisibleLineIndex());
                    if (line < firstVisible || line > firstVisible + 8)
                    {
                        return new Rect(0, (line - firstVisible) * 20, 8, 16);
                    }
                    double x = (charIndex % lineText.Length) * 8 + (trailing ? 8 : 0);
                    return new Rect(x, (line - firstVisible) * 20, 1, 16);
                });
            adorner.Measure(new Size(300, 120));
            adorner.Arrange(new Rect(0, 0, 300, 120));
            adorner.UpdateHighlights(spans);

            var drawingGroup = new DrawingGroup();
            using (DrawingContext dc = drawingGroup.Open())
            {
                InvokeOnRender(adorner, dc);
            }

            Assert.NotEmpty(drawingGroup.Children);
        });
    }

    [Fact]
    public void MainWindow_LoadsSeededSettings_And_ValidatesTimeoutEdgeCases()
    {
        var seed = new AppSettings
        {
            CurrentExpression = @"(?<word>\w+)",
            CurrentTestData = "Hello 123",
            CurrentReplacementText = "[$1]",
            SelectedOptions = RegexOptions.IgnoreCase | RegexOptions.Multiline,
            ExpandCaptures = true,
            ExpandGroups = true,
            TimeoutSeconds = 5.0,
            ExpressionList = [@"(?<word>\w+)", @"\d+"]
        };

        WithIsolatedWindow(seed, (window, _configPath) =>
        {
            Assert.Equal(@"(?<word>\w+)", window.ExpressionTextBox.Text);
            Assert.Equal("Hello 123", window.DataTextBox.Text);
            Assert.True(window.IgnoreCaseCheckBox.IsChecked);
            Assert.True(window.MultilineCheckBox.IsChecked);
            Assert.Equal("5", window.TimeoutComboBox.Text);
            Assert.Equal(TimeSpan.FromSeconds(5), window.GetConfiguredTimeout());

            window.TimeoutComboBox.Text = "10";
            Assert.Equal(TimeSpan.FromSeconds(10), window.GetConfiguredTimeout());
            window.TimeoutComboBox.Text = "0.5";
            Assert.Equal(TimeSpan.FromSeconds(0.5), window.GetConfiguredTimeout());

            window.TimeoutComboBox.Text = "invalid";
            Assert.Equal(RegexEvaluatorService.DefaultTimeout, window.GetConfiguredTimeout());
            Assert.False(window.TryGetConfiguredTimeout(out TimeSpan _));

            window.TimeoutComboBox.Text = "-1";
            Assert.Equal(RegexEvaluatorService.DefaultTimeout, window.GetConfiguredTimeout());

            window.TimeoutComboBox.Text = "999";
            Assert.Equal(RegexEvaluatorService.DefaultTimeout, window.GetConfiguredTimeout());

            window.TimeoutComboBox.Text = "";
            window.TimeoutComboBox.SelectedItem = "10";
            Assert.Equal(TimeSpan.FromSeconds(10), window.GetConfiguredTimeout());

            window.TimeoutComboBox.SelectedItem = null;
            Assert.Equal(RegexEvaluatorService.DefaultTimeout, window.GetConfiguredTimeout());
            Assert.True(window.TryGetConfiguredTimeout(out TimeSpan _));

            // TimeoutComboBox events apply crimson border when invalid
            window.TimeoutComboBox.Text = "bad";
            InvokePrivate(
                window,
                "TimeoutComboBox_TextChanged",
                window.TimeoutComboBox,
                new TextChangedEventArgs(TextBox.TextChangedEvent, UndoAction.None));
            Assert.Equal(Brushes.Crimson, window.TimeoutComboBox.BorderBrush);

            window.TimeoutComboBox.Text = "2";
            InvokePrivate(
                window,
                "TimeoutComboBox_SelectionChanged",
                window.TimeoutComboBox,
                new SelectionChangedEventArgs(ComboBox.SelectionChangedEvent, Array.Empty<object>(), Array.Empty<object>()));
            Assert.NotEqual(Brushes.Crimson, window.TimeoutComboBox.BorderBrush);
        });
    }

    [Fact]
    public void MainWindow_DisablesIncompatibleRegexOptions()
    {
        WithIsolatedWindow(null, (window, _) =>
        {
            window.NonBacktrackingCheckBox.IsChecked = true;
            Assert.False(window.RightToLeftCheckBox.IsEnabled);
            Assert.False(window.ECMAScriptCheckBox.IsEnabled);
            Assert.True(window.IgnoreCaseCheckBox.IsEnabled);

            window.NonBacktrackingCheckBox.IsChecked = false;
            Assert.True(window.RightToLeftCheckBox.IsEnabled);
            Assert.True(window.ECMAScriptCheckBox.IsEnabled);

            window.ECMAScriptCheckBox.IsChecked = true;
            Assert.False(window.ExplicitCaptureCheckBox.IsEnabled);
            Assert.False(window.IgnorePatternWhitespace.IsEnabled);
            Assert.False(window.RightToLeftCheckBox.IsEnabled);
            Assert.False(window.SinglelineCheckBox.IsEnabled);
            Assert.False(window.NonBacktrackingCheckBox.IsEnabled);
            Assert.True(window.IgnoreCaseCheckBox.IsEnabled);
            Assert.True(window.MultilineCheckBox.IsEnabled);
        });
    }

    [Fact]
    public void MainWindow_DisplaysResults_Selection_TreeExpansion_And_Errors()
    {
        WithIsolatedWindow(null, (window, _) =>
        {
            RegexEvaluationResult matchResult = RegexEvaluatorService.Evaluate(
                @"(?<word>[A-Za-z]+)|(?<num>\d+)",
                "Hello 123",
                "[$0]",
                RegexOptions.None);
            InvokePrivate(window, "DisplayResults", matchResult);

            Assert.Single(window.MatchesTreeView.Items);
            var rootItem = Assert.IsType<MatchTreeNode>(window.MatchesTreeView.Items[0]);
            Assert.Equal("Matches (2)", rootItem.Header);
            Assert.Equal("[Hello] [123]", window.ReplacementResultsTextBox.Text);

            // Large match set truncation
            var manyMatches = Enumerable.Range(0, 1005).Select(m => new MatchSnapshot(m, 1, "x", [], [])).ToList();
            var manySplits = Enumerable.Range(0, 1005).Select(_ => "item").ToList();
            var largeResult = new RegexEvaluationResult(manyMatches, "replaced", manySplits, TimeSpan.FromMilliseconds(10));
            InvokePrivate(window, "DisplayResults", largeResult);
            Assert.Contains("showing first", ((MatchTreeNode)window.MatchesTreeView.Items[0]).Header);

            // Selecting match node vs root node without span
            window.DataTextBox.Text = "Hello 123";
            var firstMatchItem = rootItem.Children[0];
            InvokePrivate(
                window,
                "MatchesTreeView_SelectedItemChanged",
                window.MatchesTreeView,
                new RoutedPropertyChangedEventArgs<object>(null!, firstMatchItem));
            Assert.Contains("Selected span:", window.StatusTextBlock.Text);

            InvokePrivate(
                window,
                "MatchesTreeView_SelectedItemChanged",
                window.MatchesTreeView,
                new RoutedPropertyChangedEventArgs<object>(firstMatchItem, rootItem));

            // Single match + in-place expansion toggles
            window.ExpandCapturesCheckBox.IsChecked = false;
            window.ExpandGroupsCheckBox.IsChecked = false;
            RegexEvaluationResult singleMatchResult = RegexEvaluatorService.Evaluate(@"Hello", "Hello 123", "Hi", RegexOptions.None);
            InvokePrivate(window, "DisplayResults", singleMatchResult);
            Assert.Contains("1 match", window.MatchSummaryTextBlock.Text);

            window.ExpandCapturesCheckBox.IsChecked = true;
            window.ExpandGroupsCheckBox.IsChecked = true;
            var refreshedRoot = Assert.IsType<MatchTreeNode>(window.MatchesTreeView.Items[0]);
            Assert.True(refreshedRoot.Children[0].Children[0].IsExpanded);
            Assert.True(refreshedRoot.Children[0].Children[1].IsExpanded);

            // Zero matches
            RegexEvaluationResult emptyResult = RegexEvaluatorService.Evaluate(@"999", "Hello 123", "", RegexOptions.None);
            InvokePrivate(window, "DisplayResults", emptyResult);
            Assert.Equal("No Matches", ((MatchTreeNode)window.MatchesTreeView.Items[0]).Header);

            // Timeout & syntax exceptions
            InvokePrivate(window, "DisplayTimeoutException", new RegexMatchTimeoutException("Hello", @"\w+", TimeSpan.FromSeconds(2)));
            Assert.Equal("Timed out", window.MatchSummaryTextBlock.Text);

            InvokePrivate(window, "DisplayException", new ArgumentException("Invalid pattern"));
            Assert.Equal("Invalid Pattern", window.MatchSummaryTextBlock.Text);

            // Empty tree update branch
            window.MatchesTreeView.ItemsSource = null;
            InvokePrivate(window, "UpdateTreeExpansionInPlace");
        });
    }

    [Fact]
    public void MainWindow_QuickInsert_SavedPatterns_Copy_And_ClearAll()
    {
        WithIsolatedWindow(null, (window, configPath) =>
        {
            // Quick-Insert wrapping
            window.ExpressionTextBox.Text = "abc";
            window.ExpressionTextBox.SelectionStart = 0;
            window.ExpressionTextBox.SelectionLength = 3;
            InvokePrivate(window, "RegexCharacterButton_Click", window.GroupButton, new RoutedEventArgs(Button.ClickEvent, window.GroupButton));
            Assert.Equal("(abc)", window.ExpressionTextBox.Text);

            // Edge cases for RegexCharacterButton_Click
            InvokePrivate(window, "RegexCharacterButton_Click", window, new RoutedEventArgs(Button.ClickEvent, window));
            var contentOnlyButton = new Button { Content = @"\w" };
            InvokePrivate(window, "RegexCharacterButton_Click", contentOnlyButton, new RoutedEventArgs(Button.ClickEvent, contentOnlyButton));
            var emptyButton = new Button();
            InvokePrivate(window, "RegexCharacterButton_Click", emptyButton, new RoutedEventArgs(Button.ClickEvent, emptyButton));

            // Save & promote MRU + editing deselects SavedExpressionsComboBox so re-pick works
            window.ExpressionTextBox.Text = "(abc)";
            InvokePrivate(window, "SaveExpressionButton_Click", window.SaveExpressionButton, new RoutedEventArgs());
            Assert.Equal("(abc)", window.SavedExpressionsComboBox.SelectedItem);

            window.ExpressionTextBox.Text = "(abc)+";
            Assert.Null(window.SavedExpressionsComboBox.SelectedItem);

            window.SavedExpressionsComboBox.SelectedItem = "(abc)";
            Assert.Equal("(abc)", window.ExpressionTextBox.Text);

            window.ExpressionTextBox.Text = @"(?<word>\w+)";
            InvokePrivate(window, "SaveExpressionButton_Click", window.SaveExpressionButton, new RoutedEventArgs());
            Assert.Equal(@"(?<word>\w+)", window.SavedExpressionsComboBox.Items[0]);

            InvokePrivate(window, "RemoveSavedExpressionButton_Click", window.RemoveSavedExpressionButton, new RoutedEventArgs());

            // Copy C# snippet
            InvokePrivate(window, "CopyCSharpButton_Click", window.CopyCSharpButton, new RoutedEventArgs());
            Assert.Contains("[GeneratedRegex", window.CSharpSnippetTextBox.Text);

            // Clear All clears inputs, snippet, options, and saved selection
            window.SavedExpressionsComboBox.SelectedItem = "(abc)";
            InvokePrivate(window, "ClearAllButton_Click", window.ClearAllButton, new RoutedEventArgs());
            Assert.Equal(string.Empty, window.ExpressionTextBox.Text);
            Assert.Equal(string.Empty, window.CSharpSnippetTextBox.Text);
            Assert.Null(window.SavedExpressionsComboBox.SelectedItem);
            Assert.False(window.IgnoreCaseCheckBox.IsChecked);
            Assert.Equal("2", window.TimeoutComboBox.Text);

            // Save with empty expression warns
            InvokePrivate(window, "SaveExpressionButton_Click", window.SaveExpressionButton, new RoutedEventArgs());
            Assert.Equal("Enter a regular expression before saving.", window.StatusTextBlock.Text);

            // Closing persists to isolated path
            window.ExpressionTextBox.Text = @"\d+";
            InvokePrivate(window, "Window_Closing", window, new System.ComponentModel.CancelEventArgs());
            AppSettings? persisted = SettingsService.Load(configPath);
            Assert.NotNull(persisted);
            Assert.Equal(@"\d+", persisted.CurrentExpression);
        });
    }

    [Fact]
    public void MainWindow_AsyncPipeline_ClearsStaleSnippetWhenExpressionCleared()
    {
        WithIsolatedWindow(null, (window, _) =>
        {
            window.ExpressionTextBox.Text = @"(?<word>\w+)";
            window.DataTextBox.Text = "Async Test 42";
            WaitWithDispatcher((Task)InvokePrivate(window, "SetupAndRunRegexWorkerAsync")!);
            Assert.Contains("[GeneratedRegex", window.CSharpSnippetTextBox.Text);

            // Clearing only DataTextBox keeps C# snippet for the expression
            window.DataTextBox.Text = string.Empty;
            WaitWithDispatcher((Task)InvokePrivate(window, "SetupAndRunRegexWorkerAsync")!);
            Assert.Contains("[GeneratedRegex", window.CSharpSnippetTextBox.Text);

            // Clearing ExpressionTextBox clears stale C# snippet
            window.ExpressionTextBox.Text = string.Empty;
            window.DataTextBox.Text = "Async Test 42";
            WaitWithDispatcher((Task)InvokePrivate(window, "SetupAndRunRegexWorkerAsync")!);
            Assert.Equal(string.Empty, window.CSharpSnippetTextBox.Text);

            // Invalid regex populates error state + C# snippet
            window.ExpressionTextBox.Text = "[invalid";
            WaitWithDispatcher((Task)InvokePrivate(window, "SetupAndRunRegexWorkerAsync")!);
            Assert.Equal("Invalid Pattern", window.MatchSummaryTextBlock.Text);

            // Invalid timeout appends warning on status bar
            window.ExpressionTextBox.Text = @"\w+";
            window.TimeoutComboBox.Text = "bad";
            WaitWithDispatcher((Task)InvokePrivate(window, "SetupAndRunRegexWorkerAsync")!);
            Assert.Contains("Invalid timeout", window.StatusTextBlock.Text);

            // DebounceTimer tick & SaveAndExitButton
            InvokePrivate(window, "DebounceTimer_Tick", window, EventArgs.Empty);
            InvokePrivate(window, "SaveAndExitButton_Click", window.SaveAndExitButton, new RoutedEventArgs());
        });
    }

    private static void InvokeOnRender(RegexMatchHighlightAdorner adorner, DrawingContext dc)
    {
        MethodInfo? onRender = typeof(RegexMatchHighlightAdorner)
            .GetMethod("OnRender", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(onRender);
        onRender.Invoke(adorner, [dc]);
    }

    private static object? InvokePrivate(object target, string methodName, params object[] args)
    {
        Type[] paramTypes = args.Select(a => a?.GetType() ?? typeof(object)).ToArray();
        MethodInfo? method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic,
            paramTypes);

        method ??= target.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == args.Length);

        Assert.NotNull(method);
        return method.Invoke(target, args);
    }
}
