using WallpaperLight.Services;

namespace WallpaperLight.Checks;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args is ["--render-ui", var output])
            {
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                UiRenderChecks.Render(Path.GetFullPath(output));
                return 0;
            }
            if (args is ["--unit"])
            {
                UnitChecks.Run();
                return 0;
            }
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            using var service = new WallpaperService();
            var before = service.GetMonitors();
            Require(before.Count > 0, "At least one connected monitor");
            Require(before.Select(m => m.DeviceId).Distinct().Count() == before.Count, "Unique device IDs");
            Require(before.All(m => m.Bounds.Width > 0 && m.Bounds.Height > 0), "Positive display dimensions");
            Require(before.All(m => !string.IsNullOrWhiteSpace(m.DeviceId)), "Nonempty IDs");

            if (args.Length == 2 && args[0] == "--apply-copy")
            {
                // Explicit opt-in only: temporarily assign an identical file at a new path.
                // The original is restored even if assertions or COM calls fail.
                var target = before.First(m => !string.IsNullOrEmpty(m.WallpaperPath));
                string copy = Path.GetFullPath(args[1]);
                Require(!File.Exists(copy), "Test copy does not overwrite an existing file");
                File.Copy(target.WallpaperPath, copy);
                try
                {
                    service.SetWallpaper(target.DeviceId, copy);
                    var after = service.GetMonitors().ToDictionary(m => m.DeviceId);
                    Require(after[target.DeviceId].WallpaperPath.Equals(copy, StringComparison.OrdinalIgnoreCase),
                        "Selected monitor received the requested file");
                    foreach (var other in before.Where(m => m.DeviceId != target.DeviceId))
                        Require(after[other.DeviceId].WallpaperPath == other.WallpaperPath,
                            "Other monitor wallpaper was preserved");
                }
                finally
                {
                    service.SetWallpaper(target.DeviceId, target.WallpaperPath);
                }
                var restored = service.GetMonitors().ToDictionary(m => m.DeviceId);
                Require(before.All(m => restored[m.DeviceId].WallpaperPath == m.WallpaperPath),
                    "All original wallpaper paths restored");
            }
            else if (args.Length != 0)
            {
                throw new ArgumentException("Usage: WallpaperLight.Checks [--apply-copy <new-jpg-path>]");
            }
            Console.WriteLine("All integration checks passed.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void Require(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + description);
        Console.WriteLine("PASS: " + description);
    }
}
