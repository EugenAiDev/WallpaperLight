using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WallpaperLight.Services;

internal sealed record DisplayDetails(string Name, string Connection, bool Primary);

internal static class MonitorDetails
{
    public static Dictionary<string, DisplayDetails> Read()
    {
        var result = new Dictionary<string, DisplayDetails>(StringComparer.OrdinalIgnoreCase);
        for (uint i = 0; ; i++)
        {
            var adapter = NewDevice();
            if (!EnumDisplayDevices(null, i, ref adapter, 0)) break;
            for (uint j = 0; ; j++)
            {
                var monitor = NewDevice();
                if (!EnumDisplayDevices(adapter.DeviceName, j, ref monitor, 1)) break;
                if (string.IsNullOrWhiteSpace(monitor.DeviceID)) continue;
                string name = ReadEdidName(monitor.DeviceID) ?? monitor.DeviceString;
                result[monitor.DeviceID] = new(name, adapter.DeviceName, (adapter.StateFlags & 4) != 0);
            }
        }
        return result;
    }

    private static string? ReadEdidName(string deviceId)
    {
        string[] parts = deviceId.Split('#');
        if (parts.Length < 3 || parts[1].Contains('\\') || parts[2].Contains('\\')) return null;
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Enum\DISPLAY\{parts[1]}\{parts[2]}\Device Parameters");
            return key?.GetValue("EDID") is byte[] bytes ? ParseEdidName(bytes) : null;
        }
        catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        { System.Diagnostics.Trace.TraceInformation(error.Message); return null; }
    }
    internal static string? ParseEdidName(byte[] bytes)
    {
        if (bytes.Length < 128 || bytes[0] != 0 || bytes[1] != 255) return null;
        for (int offset = 54; offset <= 108; offset += 18)
        {
            if (bytes[offset] != 0 || bytes[offset + 1] != 0 || bytes[offset + 2] != 0 || bytes[offset + 3] != 252) continue;
            string name = System.Text.Encoding.ASCII.GetString(bytes, offset + 5, 13).Trim('\0', ' ', '\r', '\n');
            if (name.Length > 0 && name.All(c => c >= 32 && c <= 126)) return name;
        }
        return null;
    }
    private static DisplayDevice NewDevice() => new() { Size = Marshal.SizeOf<DisplayDevice>() };
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }
    [DllImport("user32.dll", EntryPoint = "EnumDisplayDevicesW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayDevices(string? device, uint index, ref DisplayDevice display, uint flags);
}
