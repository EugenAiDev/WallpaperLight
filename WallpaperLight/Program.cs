using System.Text.Json;
using WallpaperLight.Services;
using WallpaperLight.UI;

namespace WallpaperLight;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try
        {
            // Read-only diagnostic for testing COM on real hardware without changing wallpaper.
            if (args.Length == 2 && args[0] == "--diagnose")
            {
                using var diagnosticWallpaper = new WallpaperService();
                File.WriteAllText(args[1], JsonSerializer.Serialize(diagnosticWallpaper.GetMonitors(),
                    new JsonSerializerOptions { WriteIndented = true }));
                return 0;
            }
            using var instance = new SingleInstanceService();
            if (!instance.IsPrimary) { instance.Signal(); return 0; }
            using var wallpaper = new WallpaperService();
            var settingsService = new SettingsService(AppContext.BaseDirectory);
            var settings = settingsService.Load();
            Texts.SetLanguage(settings.Settings.Language);
            using var form = new MainForm(wallpaper, settingsService, settings);
            using var context = new ApplicationContext(form);
            form.Start();
            instance.Listen(form.QueueShowFromTray);
            Application.Run(context);
            return 0;
        }
        catch (Exception error)
        {
            if (args.Length == 2 && args[0] == "--diagnose")
            {
                try
                {
                    File.WriteAllText(args[1], JsonSerializer.Serialize(new { error = error.ToString() }));
                }
                catch (Exception writeError) when (writeError is IOException or UnauthorizedAccessException)
                {
                    System.Diagnostics.Trace.TraceError(writeError.ToString());
                }
                return 1;
            }
            MessageBox.Show(Texts.Get("StartupFailed") + Environment.NewLine + error.Message,
                "Wallpaper Light", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
