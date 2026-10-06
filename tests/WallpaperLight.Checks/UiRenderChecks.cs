using WallpaperLight.Models;
using WallpaperLight.Services;
using WallpaperLight.UI;

namespace WallpaperLight.Checks;

internal static class UiRenderChecks
{
    public static void Render(string directory)
    {
        Directory.CreateDirectory(directory);
        Texts.SetLanguage("ru");
        foreach (string theme in new[] { "light", "dark" })
        {
            var settings = new AppSettings { Theme = theme, Accent = "blue", Slideshow = new() };
            var storage = new SettingsService(directory);
            using (var main = new MainForm(new PreviewWallpaper(), storage, new(settings)))
            {
                main.Start(); Application.DoEvents();
                Capture(main, Path.Combine(directory, $"main-{theme}.png"));
                main.Size = main.MinimumSize; Application.DoEvents();
                Capture(main, Path.Combine(directory, $"main-{theme}-small.png"));
                main.ExitApplication();
            }
            using var dialog = new SettingsForm(settings, false);
            dialog.Show(); Application.DoEvents();
            Capture(dialog, Path.Combine(directory, $"settings-{theme}.png"));
            dialog.Size = dialog.MinimumSize; Application.DoEvents();
            Capture(dialog, Path.Combine(directory, $"settings-{theme}-small.png"));
            dialog.Close();
        }
        Console.WriteLine("Rendered light/dark forms at default and minimum sizes without wallpaper or registry changes.");
    }
    private static void Capture(Form form, string path)
    {
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
    private sealed class PreviewWallpaper : IWallpaperService
    {
        public IReadOnlyList<MonitorInfo> GetMonitors() => new[]
        {
            new MonitorInfo("preview-one", new(0,0,2560,1440), @"C:\Wallpapers\Landscape\Mountain lake.jpg") { Name = "Q27G4ZD", Connection = @"\\.\DISPLAY1", IsPrimary = true },
            new MonitorInfo("preview-two", new(2560,-1346,2160,3840), @"C:\Wallpapers\Portrait\Forest.jpg") { Name = "LG ULTRAFINE", Connection = @"\\.\DISPLAY2" }
        };
        public void SetWallpaper(string monitorId, string path) => throw new InvalidOperationException("Render-only fixture.");
    }
}
