using System.IO;
using System.Text.Json;

namespace CSharpRegexTester;

public class RegexTesterConfigurationFile
{
    public List<string> ExpressionList { get; set; } = [];
    public string CurrentExpression { get; set; } = string.Empty;
    public string CurrentReplacementText { get; set; } = string.Empty;
    public string CurrentTestData { get; set; } = string.Empty;

    private const string CFG_FILE_NAME = "RegExTester.cfg";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static RegexTesterConfigurationFile? Load()
    {
        if (!File.Exists(CFG_FILE_NAME))
        {
            return null;
        }

        try
        {
            using FileStream configStream = File.OpenRead(CFG_FILE_NAME);
            return JsonSerializer.Deserialize<RegexTesterConfigurationFile>(configStream, JsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Save()
    {
        using FileStream configStream = File.Create(CFG_FILE_NAME);
        JsonSerializer.Serialize(configStream, this, JsonOptions);
        configStream.Flush();
    }
}
