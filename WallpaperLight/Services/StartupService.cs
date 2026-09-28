using Microsoft.Win32;

namespace WallpaperLight.Services;

internal interface IStartupStore
{
    string? Read();
    void Write(string command);
    void Delete();
}

internal sealed class StartupService(string executable, IStartupStore? store = null)
{
    private readonly IStartupStore store = store ?? new RegistryStartupStore();
    private readonly string command = BuildCommand(executable);
    public bool Enabled => string.Equals(store.Read(), command, StringComparison.OrdinalIgnoreCase);

    internal static string BuildCommand(string executable)
    {
        if (!Path.IsPathFullyQualified(executable) || executable.Contains('"'))
            throw new ArgumentException("Invalid executable path.", nameof(executable));
        string result = "\"" + Path.GetFullPath(executable) + "\"";
        if (result.Length > 260) throw new ArgumentException("Startup command exceeds 260 characters.");
        return result;
    }

    public void SetEnabled(bool enabled)
    {
        string? existing = store.Read();
        if (string.Equals(existing, command, StringComparison.OrdinalIgnoreCase))
        {
            if (!enabled) store.Delete();
            return;
        }
        if (!enabled) return;
        // An older portable location can be replaced, but never an unrelated command.
        if (existing is not null && !(existing.Length > 2 && existing[0] == '"' && existing[^1] == '"' &&
            !existing[1..^1].Contains('"') && Path.IsPathFullyQualified(existing[1..^1]) &&
            string.Equals(Path.GetFileName(existing[1..^1]), "WallpaperLight.exe", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(UI.Texts.Get("StartupConflict"));
        store.Write(command);
    }

    private sealed class RegistryStartupStore : IStartupStore
    {
        private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string Name = "WallpaperLight";
        public string? Read()
        {
            using var key = Registry.CurrentUser.OpenSubKey(Key);
            var value = key?.GetValue(Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            return value is null ? null : value as string ?? "<unsupported registry value>";
        }
        public void Write(string command)
        {
            using var key = Registry.CurrentUser.CreateSubKey(Key, true);
            key.SetValue(Name, command, RegistryValueKind.String);
        }
        public void Delete()
        {
            using var key = Registry.CurrentUser.OpenSubKey(Key, true);
            key?.DeleteValue(Name, false);
        }
    }
}
