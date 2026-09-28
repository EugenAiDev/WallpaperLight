using System.Runtime.InteropServices;

namespace WallpaperLight.Native;

// The first five slots of IDesktopWallpaper, in Windows SDK vtable order.
// Do not reorder these declarations or insert methods between them.
// LPWSTR results use the COM task allocator; services release them explicitly.
[ComImport]
[Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDesktopWallpaper
{
    [PreserveSig]
    int SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorId,
        [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);

    [PreserveSig]
    int GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorId, out IntPtr wallpaper);

    [PreserveSig]
    int GetMonitorDevicePathAt(uint monitorIndex, out IntPtr monitorId);

    [PreserveSig]
    int GetMonitorDevicePathCount(out uint count);

    [PreserveSig]
    int GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorId, out NativeRect displayRect);
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeRect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public readonly Rectangle ToRectangle() => Rectangle.FromLTRB(Left, Top, Right, Bottom);
}
