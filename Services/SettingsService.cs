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

    public static readonly string FallbackConfigPath = Path.Combine(
        AppContext.BaseDirectory,
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
                : File.Exists(FallbackConfigPath)
                    ? FallbackConfigPath
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

    public static bool Save(AppSettings settings, string? customPath = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string targetPath = customPath ?? AppDataConfigPath;
        return TryWriteAtomic(targetPath, settings) || TryWriteAtomic(FallbackConfigPath, settings);
    }

    private static bool TryWriteAtomic(string targetPath, AppSettings settings)
    {
        string? tempPath = null;
        try
        {
            string? directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string fileName = Path.GetFileName(targetPath);
            if (string.IsNullOrEmpty(fileName) || targetPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                return false;
            }

            tempPath = Path.Combine(directory ?? AppContext.BaseDirectory, $".{fileName}.{Guid.NewGuid():N}.tmp");
            using (FileStream configStream = File.Create(tempPath))
            {
                JsonSerializer.Serialize(configStream, settings, JsonOptions);
                configStream.Flush();
            }

            File.Move(tempPath, targetPath, overwrite: true);
            tempPath = null;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            if (tempPath is not null && File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (Exception)
                {
                    // Best-effort cleanup of orphaned temp file
                }
            }
        }
    }
}
