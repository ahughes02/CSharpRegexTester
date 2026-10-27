using System.IO;
using System.Text.Json;
using CSharpRegexTester.Models;

namespace CSharpRegexTester.Services;

/// <summary>
/// Handles loading and saving application settings as JSON in LocalApplicationData with a local file fallback.
/// </summary>
public static class SettingsService
{
    private const string ConfigFileName = "RegExTester.cfg";

    private static readonly string AppDataConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CSharpRegexTester",
        ConfigFileName);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static AppSettings? Load()
    {
        string? pathToLoad = File.Exists(AppDataConfigPath)
            ? AppDataConfigPath
            : File.Exists(ConfigFileName)
                ? ConfigFileName
                : null;

        if (pathToLoad is null)
        {
            return null;
        }

        try
        {
            using FileStream configStream = File.OpenRead(pathToLoad);
            return JsonSerializer.Deserialize<AppSettings>(configStream, JsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            string? directory = Path.GetDirectoryName(AppDataConfigPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using FileStream configStream = File.Create(AppDataConfigPath);
            JsonSerializer.Serialize(configStream, settings, JsonOptions);
            configStream.Flush();
        }
        catch (Exception)
        {
            using FileStream fallbackStream = File.Create(ConfigFileName);
            JsonSerializer.Serialize(fallbackStream, settings, JsonOptions);
            fallbackStream.Flush();
        }
    }
}
