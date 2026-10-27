using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CSharpRegexTester;

public class RegexTesterConfigurationFile
{
    public List<string> ExpressionList { get; set; } = [];
    public string CurrentExpression { get; set; } = string.Empty;
    public string CurrentReplacementText { get; set; } = string.Empty;
    public string CurrentTestData { get; set; } = string.Empty;
    public RegexOptions SelectedOptions { get; set; } = RegexOptions.None;
    public bool ExpandCaptures { get; set; }
    public bool ExpandGroups { get; set; }

    private const string CFG_FILE_NAME = "RegExTester.cfg";

    private static readonly string AppDataCfgPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CSharpRegexTester",
        CFG_FILE_NAME);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static RegexTesterConfigurationFile? Load()
    {
        string? pathToLoad = File.Exists(AppDataCfgPath)
            ? AppDataCfgPath
            : File.Exists(CFG_FILE_NAME)
                ? CFG_FILE_NAME
                : null;

        if (pathToLoad is null)
        {
            return null;
        }

        try
        {
            using FileStream configStream = File.OpenRead(pathToLoad);
            return JsonSerializer.Deserialize<RegexTesterConfigurationFile>(configStream, JsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Save()
    {
        try
        {
            string? directory = Path.GetDirectoryName(AppDataCfgPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using FileStream configStream = File.Create(AppDataCfgPath);
            JsonSerializer.Serialize(configStream, this, JsonOptions);
            configStream.Flush();
        }
        catch (Exception)
        {
            using FileStream fallbackStream = File.Create(CFG_FILE_NAME);
            JsonSerializer.Serialize(fallbackStream, this, JsonOptions);
            fallbackStream.Flush();
        }
    }
}
