using WallpaperLight.Models;

namespace WallpaperLight.Services;

internal interface IWallpaperService
{
    IReadOnlyList<MonitorInfo> GetMonitors();
    void SetWallpaper(string monitorId, string path);
}
