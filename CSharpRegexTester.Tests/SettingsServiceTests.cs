using System.IO;
using System.Text.RegularExpressions;
using CSharpRegexTester.Models;
using CSharpRegexTester.Services;

namespace CSharpRegexTester.Tests;

public class SettingsServiceTests
{
    [Fact]
    public void SaveAndLoad_WithCustomTempFile_RoundTripsAllProperties()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CSharpRegexTesterTests", Guid.NewGuid().ToString("N"));
        string tempFile = Path.Combine(tempDir, "test_settings.cfg");

        try
        {
            var expected = new AppSettings
            {
                CurrentExpression = @"(?<word>\w+)",
                CurrentReplacementText = "[$1]",
                CurrentTestData = "hello world",
                SelectedOptions = RegexOptions.IgnoreCase | RegexOptions.Multiline,
                ExpandCaptures = true,
                ExpandGroups = true,
                ExpressionList = [@"\d+", @"\w+"]
            };

            SettingsService.Save(expected, tempFile);
            AppSettings? loaded = SettingsService.Load(tempFile);

            Assert.NotNull(loaded);
            Assert.Equal(expected.CurrentExpression, loaded.CurrentExpression);
            Assert.Equal(expected.CurrentReplacementText, loaded.CurrentReplacementText);
            Assert.Equal(expected.CurrentTestData, loaded.CurrentTestData);
            Assert.Equal(expected.SelectedOptions, loaded.SelectedOptions);
            Assert.True(loaded.ExpandCaptures);
            Assert.True(loaded.ExpandGroups);
            Assert.Equal(expected.ExpressionList, loaded.ExpressionList);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void Load_WhenFileDoesNotExistOrIsInvalidJson_ReturnsNull()
    {
        string missingFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.cfg");
        Assert.Null(SettingsService.Load(missingFile));

        string corruptFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(corruptFile, "{ not valid json ");
            Assert.Null(SettingsService.Load(corruptFile));
        }
        finally
        {
            File.Delete(corruptFile);
        }
    }

    [Fact]
    public void Save_WithInvalidPath_FallsBackToLocalConfigFile()
    {
        var settings = new AppSettings { CurrentExpression = @"\bword\b" };
        // Invalid path containing null character triggers fallback
        string invalidPath = "invalid\0path.cfg";

        try
        {
            SettingsService.Save(settings, invalidPath);
            Assert.True(File.Exists(SettingsService.ConfigFileName));
        }
        finally
        {
            if (File.Exists(SettingsService.ConfigFileName))
            {
                File.Delete(SettingsService.ConfigFileName);
            }
        }
    }
}
