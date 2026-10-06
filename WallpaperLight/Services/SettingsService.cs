using System.Text.Json;
using System.Text.Json.Serialization;
using WallpaperLight.Models;

namespace WallpaperLight.Services;

internal sealed record SettingsLoadResult(AppSettings Settings, string? WarningKey = null, string? Detail = null);

internal sealed class SettingsService(string applicationDirectory)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };
    public string FilePath { get; } = Path.Combine(applicationDirectory, "data", "settings.json");
    public bool IsReadOnly { get; private set; }

    public SettingsLoadResult Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                var defaults = new AppSettings();
                Save(defaults);
                return new(defaults);
            }
            string json = File.ReadAllText(FilePath);
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("schemaVersion", out var version) &&
                version.ValueKind == JsonValueKind.Number && version.TryGetInt32(out int number) && number > 1)
            {
                IsReadOnly = true;
                return new(new AppSettings(), "NewerSettings", FilePath);
            }
            return new(Validate(JsonSerializer.Deserialize<AppSettings>(json, Options)
                ?? throw new JsonException("Settings must be an object.")));
        }
        catch (Exception error) when (error is JsonException or InvalidDataException)
        {
            try
            {
                string backup = FilePath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfffffff") + ".json";
                File.Copy(FilePath, backup, overwrite: false);
                foreach (string old in Directory.GetFiles(Path.GetDirectoryName(FilePath)!, "settings.json.corrupt-*.json")
                    .OrderByDescending(p => p, StringComparer.Ordinal).Skip(3))
                    File.Delete(old);
                var defaults = new AppSettings();
                Save(defaults);
                return new(defaults, "RecoveredSettings", backup);
            }
            catch (Exception recoveryError) when (IsStorageError(recoveryError))
            {
                IsReadOnly = true;
                return new(new AppSettings(), "SettingsUnavailable", recoveryError.Message);
            }
        }
        catch (Exception error) when (IsStorageError(error))
        {
            IsReadOnly = true;
            return new(new AppSettings(), "SettingsUnavailable", error.Message);
        }
    }

    public void Save(AppSettings settings)
    {
        if (IsReadOnly) throw new InvalidOperationException("Settings are read-only for this session.");
        settings = Validate(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string temporary = FilePath + ".tmp";
        // Overwrite a leftover temp file on the next save. A failed replace leaves
        // the old settings intact; the temp file is never read as configuration.
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, settings, Options);
            stream.Flush(flushToDisk: true);
        }
        if (File.Exists(FilePath)) File.Replace(temporary, FilePath, FilePath + ".bak");
        else File.Move(temporary, FilePath);
    }

    private static AppSettings Validate(AppSettings settings)
    {
        if (settings.SchemaVersion != 1 || settings.Language is not ("system" or "ru" or "en") ||
            settings.Theme is not ("system" or "light" or "dark") ||
            settings.Accent is not ("system" or "blue" or "teal" or "violet") ||
            settings.Slideshow is null || !Enum.IsDefined(settings.Slideshow.Mode) ||
            settings.Slideshow.CustomMinutes is < 1 or > 1440 ||
            settings.Folders is null || settings.Folders.Landscape is null || settings.Folders.Portrait is null ||
            settings.Monitors is null || settings.Monitors.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != settings.Monitors.Count ||
            settings.Monitors.Any(pair => string.IsNullOrWhiteSpace(pair.Key) ||
                pair.Value is null || !Enum.IsDefined(pair.Value.Mode)))
            throw new InvalidDataException("Invalid settings values.");
        return settings with
        {
            Monitors = new Dictionary<string, MonitorSettings>(settings.Monitors, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static bool IsStorageError(Exception error) =>
        error is IOException or UnauthorizedAccessException or System.Security.SecurityException;
}
