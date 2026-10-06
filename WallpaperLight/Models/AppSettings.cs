using System.Text.Json;
using System.Text.Json.Serialization;

namespace WallpaperLight.Models;

internal enum PlaybackMode { Sequential, Random }
internal enum IntervalMode { ThirtyMinutes, SixtyMinutes, Custom, Random }

internal sealed record SlideshowSettings
{
    public bool Enabled { get; init; } = true;
    public IntervalMode Mode { get; init; } = IntervalMode.ThirtyMinutes;
    public int CustomMinutes { get; init; } = 30;
}

internal sealed record WallpaperFolders
{
    public string Landscape { get; init; } = "";
    public string Portrait { get; init; } = "";
    public string For(bool portrait) => portrait ? Portrait : Landscape;
}

internal sealed record MonitorSettings
{
    public PlaybackMode Mode { get; init; } = PlaybackMode.Random;
}

internal sealed record AppSettings
{
    public int SchemaVersion { get; init; } = 1;
    public string Language { get; init; } = "system";
    public bool StartMinimized { get; init; }
    public string Theme { get; init; } = "system";
    public string Accent { get; init; } = "system";
    public SlideshowSettings Slideshow { get; init; } = new();
    public WallpaperFolders Folders { get; init; } = new();
    public Dictionary<string, MonitorSettings> Monitors { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }

    public PlaybackMode ModeFor(string deviceId) =>
        Monitors.TryGetValue(deviceId, out var monitor) ? monitor.Mode : PlaybackMode.Random;
}
