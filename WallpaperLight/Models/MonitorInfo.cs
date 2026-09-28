namespace WallpaperLight.Models;

internal sealed record MonitorInfo(string DeviceId, Rectangle Bounds, string WallpaperPath)
{
    public bool IsPortrait => Bounds.Height > Bounds.Width;
    public string Name { get; init; } = "";
    public string Connection { get; init; } = "";
    public bool IsPrimary { get; init; }
}
