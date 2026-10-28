using System.IO;
using System.Text.Json;
using CSharpRegexTester.Models;

namespace CSharpRegexTester.Services;

/// <summary>
/// Handles loading and saving application settings as JSON in LocalApplicationData with a local file fallback.
/// </summary>
public static class SettingsService
{
    public const string ConfigFileName = "RegExTester.cfg";

    public static readonly string AppDataConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CSharpRegexTester",
        ConfigFileName);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static AppSettings? Load(string? customPath = null)
    {
        string? pathToLoad = customPath is not null
            ? (File.Exists(customPath) ? customPath : null)
            : File.Exists(AppDataConfigPath)
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
            using var configStream = new FileStream(pathToLoad, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return JsonSerializer.Deserialize<AppSettings>(configStream, JsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static void Save(AppSettings settings, string? customPath = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string targetPath = customPath ?? AppDataConfigPath;

        try
        {
            string? directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using FileStream configStream = File.Create(targetPath);
            JsonSerializer.Serialize(configStream, settings, JsonOptions);
            configStream.Flush();
        }
        catch (Exception)
        {
            try
            {
                using FileStream fallbackStream = File.Create(ConfigFileName);
                JsonSerializer.Serialize(fallbackStream, settings, JsonOptions);
                fallbackStream.Flush();
            }
            catch (Exception)
            {
                // Silently ignore if both AppData and local directory are not writable (e.g. read-only environments)
            }
        }
    }
}
