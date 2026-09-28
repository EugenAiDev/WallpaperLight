using System.Runtime.InteropServices;
using WallpaperLight.Models;
using WallpaperLight.Native;

namespace WallpaperLight.Services;

internal sealed class WallpaperService : IWallpaperService, IDisposable
{
    private readonly int ownerThreadId = Environment.CurrentManagedThreadId;
    private IDesktopWallpaper? desktop;

    public WallpaperService()
    {
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            throw new InvalidOperationException("Desktop wallpaper service requires an STA thread.");

        var type = Type.GetTypeFromCLSID(new Guid("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD"), true)!;
        desktop = (IDesktopWallpaper)Activator.CreateInstance(type)!;
    }

    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var api = GetApi();
        Marshal.ThrowExceptionForHR(api.GetMonitorDevicePathCount(out uint count));
        var monitors = new List<MonitorInfo>();
        var details = MonitorDetails.Read();
        for (uint index = 0; index < count; index++)
        {
            IntPtr idPointer = IntPtr.Zero;
            string id;
            try
            {
                Marshal.ThrowExceptionForHR(api.GetMonitorDevicePathAt(index, out idPointer));
                id = Marshal.PtrToStringUni(idPointer) ?? throw new InvalidOperationException("Empty monitor ID.");
            }
            finally { Marshal.FreeCoTaskMem(idPointer); }

            int result = api.GetMonitorRECT(id, out var bounds);
            if (result == 1) continue; // S_FALSE: Windows remembers a disconnected display.
            Marshal.ThrowExceptionForHR(result);
            if (bounds.Right <= bounds.Left || bounds.Bottom <= bounds.Top) continue;

            IntPtr wallpaperPointer = IntPtr.Zero;
            try
            {
                Marshal.ThrowExceptionForHR(api.GetWallpaper(id, out wallpaperPointer));
                details.TryGetValue(id, out var detail);
                monitors.Add(new MonitorInfo(id, bounds.ToRectangle(),
                    Marshal.PtrToStringUni(wallpaperPointer) ?? string.Empty)
                { Name = detail?.Name ?? "", Connection = detail?.Connection ?? "", IsPrimary = detail?.Primary ?? false });
            }
            finally { Marshal.FreeCoTaskMem(wallpaperPointer); }
        }
        return monitors;
    }

    public void SetWallpaper(string monitorId, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(monitorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        WallpaperImage.Validate(fullPath);

        var api = GetApi();
        int result = api.GetMonitorRECT(monitorId, out _);
        if (result == 1) throw new InvalidOperationException(UI.Texts.Get("MonitorDisconnected"));
        Marshal.ThrowExceptionForHR(result);
        Marshal.ThrowExceptionForHR(api.SetWallpaper(monitorId, fullPath));
    }

    private IDesktopWallpaper GetApi()
    {
        if (Environment.CurrentManagedThreadId != ownerThreadId)
            throw new InvalidOperationException("Use the wallpaper service on its owning STA thread.");
        return desktop ?? throw new ObjectDisposedException(nameof(WallpaperService));
    }

    public void Dispose()
    {
        if (desktop is null) return;
        var api = GetApi();
        desktop = null;
        // This RCW is private to this service and never handed to another thread.
        Marshal.ReleaseComObject(api);
    }
}
