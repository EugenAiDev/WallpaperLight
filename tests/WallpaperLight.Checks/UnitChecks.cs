using System.Text.Json;
using WallpaperLight.Models;
using WallpaperLight.Services;
using WallpaperLight.UI;

namespace WallpaperLight.Checks;

internal static class UnitChecks
{
    private static int count;

    public static void Run()
    {
        string temporary = Path.Combine(Path.GetTempPath(), "WallpaperLight-checks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            Check("Settings defaults and round trip", () =>
            {
                var service = new SettingsService(Path.Combine(temporary, "roundtrip"));
                var loaded = service.Load();
                Assert(loaded.WarningKey is null && loaded.Settings.Language == "system");
                service.Save(loaded.Settings with
                {
                    Language = "en", Folders = new() { Landscape = @"C:\Обои\Horizontal", Portrait = @"C:\Обои\Vertical" },
                    Monitors = new() { ["device"] = new() { Mode = PlaybackMode.Sequential } }
                });
                var saved = service.Load().Settings;
                Assert(saved.Language == "en" && saved.Folders.Portrait == @"C:\Обои\Vertical");
                Assert(saved.ModeFor("DEVICE") == PlaybackMode.Sequential);
                Assert(File.Exists(service.FilePath + ".bak") && !File.Exists(service.FilePath + ".tmp"));
            });
            Check("Corrupt settings backed up and defaults restored", () =>
            {
                var service = new SettingsService(Path.Combine(temporary, "corrupt"));
                service.Load();
                for (int i = 0; i < 5; i++)
                {
                    File.WriteAllText(service.FilePath, "{broken");
                    var loaded = service.Load();
                    Assert(loaded.WarningKey == "RecoveredSettings" && loaded.Settings.Language == "system");
                }
                var backups = Directory.GetFiles(Path.GetDirectoryName(service.FilePath)!, "*.corrupt-*.json");
                Assert(backups.Length == 3 && backups.All(p => File.ReadAllText(p) == "{broken"));
            });
            Check("Future schema is never overwritten", () =>
            {
                var service = new SettingsService(Path.Combine(temporary, "future"));
                service.Load();
                string future = "{\"schemaVersion\":999,\"futureData\":true}";
                File.WriteAllText(service.FilePath, future);
                Assert(service.Load().WarningKey == "NewerSettings" && service.IsReadOnly);
                Throws<InvalidOperationException>(() => service.Save(new()));
                Assert(File.ReadAllText(service.FilePath) == future);
            });
            Check("Invalid fields safely recover", () =>
            {
                foreach (string invalid in new[] { "null", "[]", "{\"folders\":null}", "{\"language\":\"xx\"}",
                    "{\"schemaVersion\":\"x\"}", "{\"monitors\":{\"x\":{\"mode\":\"Unknown\"}}}",
                    "{\"monitors\":{\"x\":{},\"X\":{}}}" })
                {
                    var service = new SettingsService(Path.Combine(temporary, Guid.NewGuid().ToString("N")));
                    service.Load();
                    File.WriteAllText(service.FilePath, invalid);
                    Assert(service.Load().WarningKey == "RecoveredSettings");
                }
            });
            Check("Failed replacement preserves original settings", () =>
            {
                var service = new SettingsService(Path.Combine(temporary, "atomic"));
                service.Load();
                string original = File.ReadAllText(service.FilePath);
                using (File.Open(service.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    Throws<IOException>(() => service.Save(new() { Language = "en" }));
                Assert(File.ReadAllText(service.FilePath) == original);
                service.Save(new() { Language = "ru" });
                Assert(service.Load().Settings.Language == "ru");
            });
            Check("Storage failure produces read-only session", () =>
            {
                string basePath = Path.Combine(temporary, "not-directory");
                File.WriteAllText(basePath, "file");
                var service = new SettingsService(basePath);
                Assert(service.Load().WarningKey == "SettingsUnavailable" && service.IsReadOnly);
            });
            Check("Unknown JSON properties survive saving", () =>
            {
                var service = new SettingsService(Path.Combine(temporary, "extensions"));
                service.Load();
                File.WriteAllText(service.FilePath, "{\"autoChangeEnabled\":false}");
                service.Save(service.Load().Settings with { Language = "en" });
                using var json = JsonDocument.Parse(File.ReadAllText(service.FilePath));
                Assert(!json.RootElement.GetProperty("autoChangeEnabled").GetBoolean());
            });
            Check("Folder filtering, sorting, shared cache and refresh", () =>
            {
                string folder = Path.Combine(temporary, "images");
                Directory.CreateDirectory(Path.Combine(folder, "subfolder"));
                foreach (string name in new[] { "b.JPG", "A.png", "c.bmp", "d.jpeg", "ignore.gif", "ignore.txt", "hidden.jpg" })
                    File.WriteAllText(Path.Combine(folder, name), "fixture");
                File.SetAttributes(Path.Combine(folder, "hidden.jpg"), FileAttributes.Hidden);
                File.WriteAllText(Path.Combine(folder, "subfolder", "nested.jpg"), "fixture");
                var catalog = new WallpaperCatalog();
                var first = catalog.GetAsync(folder).GetAwaiter().GetResult();
                Assert(first.Select(Path.GetFileName).SequenceEqual(new[] { "A.png", "b.JPG", "c.bmp", "d.jpeg" }));
                File.WriteAllText(Path.Combine(folder, "e.png"), "fixture");
                Assert(ReferenceEquals(first, catalog.GetAsync(folder).GetAwaiter().GetResult()));
                catalog.Refresh();
                Assert(catalog.GetAsync(folder).GetAwaiter().GetResult().Count == 5);
                using var cancellation = new CancellationTokenSource();
                cancellation.Cancel();
                catalog.Refresh();
                Throws<OperationCanceledException>(() => catalog.GetAsync(folder, cancellation.Token).GetAwaiter().GetResult());
            });
            Check("Image validation rejects corrupt data and releases file handles", () =>
            {
                foreach (var (extension, format) in new[]
                {
                    (".png", System.Drawing.Imaging.ImageFormat.Png),
                    (".jpg", System.Drawing.Imaging.ImageFormat.Jpeg),
                    (".bmp", System.Drawing.Imaging.ImageFormat.Bmp)
                })
                {
                    string path = Path.Combine(temporary, "valid" + extension);
                    using (var image = new Bitmap(8, 8)) image.Save(path, format);
                    WallpaperImage.Validate(path);
                    using var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                }
                string broken = Path.Combine(temporary, "broken.png");
                File.WriteAllText(broken, "not an image");
                Throws<InvalidDataException>(() => WallpaperImage.Validate(broken));
                Throws<FileNotFoundException>(() => WallpaperImage.Validate(Path.Combine(temporary, "missing.jpg")));
                Throws<NotSupportedException>(() => WallpaperImage.Validate(Path.Combine(temporary, "not-supported.gif")));
            });
            Check("Manual choice joins history and Next branches after Previous", () =>
            {
                var fake = new FakeWallpaper();
                var navigator = new WallpaperNavigator(fake);
                string[] files = { Path.Combine(temporary, "a.jpg"), Path.Combine(temporary, "b.jpg"), Path.Combine(temporary, "c.jpg") };
                navigator.Choose(fake.Monitor(), files[0]);
                navigator.Choose(fake.Monitor(), files[1]);
                navigator.Choose(fake.Monitor(), files[2]);
                navigator.Change(fake.Monitor(), files, PlaybackMode.Sequential, WallpaperCommand.Previous);
                navigator.Change(fake.Monitor(), files, PlaybackMode.Sequential, WallpaperCommand.Previous);
                navigator.Choose(fake.Monitor(), files[2]);
                navigator.Change(fake.Monitor(), files, PlaybackMode.Sequential, WallpaperCommand.Previous);
                Assert(fake.Current["one"] == files[0]);
                Assert(navigator.Change(fake.Monitor(), files, PlaybackMode.Sequential, WallpaperCommand.Previous).MessageKey == "NoPrevious");
            });
            Check("Sequential wraps and Previous follows successful history", () =>
            {
                var fake = new FakeWallpaper();
                var navigator = new WallpaperNavigator(fake);
                string[] files = { "a.jpg", "b.jpg", "c.jpg" };
                foreach (string expected in new[] { "a.jpg", "b.jpg", "c.jpg", "a.jpg" })
                {
                    navigator.Change(fake.Monitor(), files, PlaybackMode.Sequential, WallpaperCommand.Next);
                    Assert(fake.Current["one"] == expected);
                }
                navigator.Change(fake.Monitor(), files, PlaybackMode.Sequential, WallpaperCommand.Previous);
                Assert(fake.Current["one"] == "c.jpg");
                navigator.Change(fake.Monitor(), files, PlaybackMode.Sequential, WallpaperCommand.Next);
                Assert(fake.Current["one"] == "a.jpg");
            });
            Check("Random has complete cycles and no boundary repeat", () =>
            {
                var fake = new FakeWallpaper();
                var navigator = new WallpaperNavigator(fake, new Random(31));
                string[] files = { "a.jpg", "b.jpg", "c.jpg", "d.jpg", "e.jpg" };
                string last = "";
                for (int cycle = 0; cycle < 30; cycle++)
                {
                    var seen = new HashSet<string>();
                    for (int i = 0; i < files.Length; i++)
                    {
                        navigator.Change(fake.Monitor(), files, PlaybackMode.Random, WallpaperCommand.Next);
                        string current = fake.Current["one"];
                        Assert(current != last && seen.Add(current));
                        last = current;
                    }
                    Assert(seen.SetEquals(files));
                }
            });
            Check("Empty and single file do not repeatedly apply", () =>
            {
                var fake = new FakeWallpaper();
                var navigator = new WallpaperNavigator(fake);
                Assert(navigator.Change(fake.Monitor(), Array.Empty<string>(), PlaybackMode.Random, WallpaperCommand.Next).MessageKey == "NoImages");
                navigator.Change(fake.Monitor(), new[] { "only.jpg" }, PlaybackMode.Random, WallpaperCommand.Next);
                int calls = fake.Calls;
                Assert(navigator.Change(fake.Monitor(), new[] { "only.jpg" }, PlaybackMode.Random, WallpaperCommand.Next).MessageKey == "AlreadyCurrent");
                Assert(fake.Calls == calls);
            });
            Check("Broken files skipped with bounded attempts and retried after Refresh", () =>
            {
                var fake = new FakeWallpaper();
                fake.Rejected.UnionWith(new[] { "a.jpg", "b.jpg" });
                var navigator = new WallpaperNavigator(fake);
                string[] files = { "a.jpg", "b.jpg" };
                Assert(navigator.Change(fake.Monitor(), files, PlaybackMode.Random, WallpaperCommand.Next).MessageKey == "NoUsableImages");
                Assert(fake.Calls == 2);
                navigator.Change(fake.Monitor(), files, PlaybackMode.Random, WallpaperCommand.Next);
                Assert(fake.Calls == 2);
                fake.Rejected.Clear();
                navigator.RefreshCandidates();
                Assert(navigator.Change(fake.Monitor(), files, PlaybackMode.Random, WallpaperCommand.Next).MessageKey == "WallpaperApplied");
            });
            Check("History skips deleted files and does not record failed changes", () =>
            {
                var fake = new FakeWallpaper();
                var navigator = new WallpaperNavigator(fake);
                string[] files = { "a.jpg", "b.jpg", "c.jpg" };
                for (int i = 0; i < 3; i++) navigator.Change(fake.Monitor(), files, PlaybackMode.Sequential, WallpaperCommand.Next);
                fake.Rejected.Add("b.jpg");
                navigator.Change(fake.Monitor(), files, PlaybackMode.Sequential, WallpaperCommand.Previous);
                Assert(fake.Current["one"] == "a.jpg");
                Assert(navigator.Change(fake.Monitor(), files, PlaybackMode.Sequential, WallpaperCommand.Previous).MessageKey == "NoPrevious");
            });
            Check("Monitor and orientation histories are independent", () =>
            {
                var fake = new FakeWallpaper();
                var navigator = new WallpaperNavigator(fake, new Random(3));
                string[] files = { "a.jpg", "b.jpg" };
                navigator.Change(fake.Monitor(), files, PlaybackMode.Sequential, WallpaperCommand.Next);
                navigator.Change(fake.Monitor(), files, PlaybackMode.Sequential, WallpaperCommand.Next);
                Assert(navigator.CanGoBack(fake.Monitor()));
                Assert(!navigator.CanGoBack(fake.Monitor("two")));
                Assert(!navigator.CanGoBack(fake.Monitor(portrait: true)));
                navigator.Change(fake.Monitor("two"), files, PlaybackMode.Sequential, WallpaperCommand.Next);
                Assert(fake.Current["one"] == "b.jpg" && fake.Current["two"] == "a.jpg");
                navigator.ResetOrientation(true);
                Assert(navigator.CanGoBack(fake.Monitor()));
                navigator.ResetOrientation(false);
                Assert(!navigator.CanGoBack(fake.Monitor()));
            });
            Check("History is limited to 50 entries", () =>
            {
                var fake = new FakeWallpaper();
                var navigator = new WallpaperNavigator(fake);
                string[] files = { "a.jpg", "b.jpg", "c.jpg" };
                for (int i = 0; i < 70; i++) navigator.Change(fake.Monitor(), files, PlaybackMode.Sequential, WallpaperCommand.Next);
                int steps = 0;
                while (navigator.Change(fake.Monitor(), files, PlaybackMode.Sequential, WallpaperCommand.Previous).MessageKey == "WallpaperApplied") steps++;
                Assert(steps == 49);
            });
            Check("Language switches and shared folder mapping", () =>
            {
                Texts.SetLanguage("en");
                Assert(Texts.Get("Settings") == "Settings");
                Texts.SetLanguage("ru");
                Assert(Texts.Get("Settings") == "Настройки");
                Texts.SetLanguage("system");
                var folders = new WallpaperFolders { Landscape = "horizontal", Portrait = "vertical" };
                Assert(folders.For(false) == "horizontal" && folders.For(true) == "vertical");
            });
            Check("Theme persistence and reversible control colors", () =>
            {
                var service = new SettingsService(Path.Combine(temporary, "themes"));
                Assert(service.Load().Settings.Theme == "system");
                service.Save(new AppSettings { Theme = "dark" });
                Assert(service.Load().Settings.Theme == "dark");
                Throws<InvalidDataException>(() => service.Save(new AppSettings { Theme = "invalid" }));
                using var form = new Form();
                var field = new TextBox();
                form.Controls.Add(field);
                Theme.Apply(form, "dark");
                if (!SystemInformation.HighContrast) Assert(field.BackColor.GetBrightness() < 0.3 && field.ForeColor.GetBrightness() > 0.8);
                Theme.Apply(form, "light");
                Assert(field.BackColor == SystemColors.Window);
            });
            Check("Monitor model from EDID with safe fallback", () =>
            {
                Assert(MonitorDetails.ParseEdidName(new byte[5]) is null);
                byte[] edid = new byte[128];
                edid[1] = 255;
                edid[57] = 252;
                System.Text.Encoding.ASCII.GetBytes("TEST DISPLAY\n").CopyTo(edid, 59);
                Assert(MonitorDetails.ParseEdidName(edid) == "TEST DISPLAY");
                edid[57] = 0;
                Assert(MonitorDetails.ParseEdidName(edid) is null);
            });
            Check("Startup command quoting and foreign entry protection", () =>
            {
                var store = new FakeStartupStore();
                var service = new StartupService(@"C:\Обои Light\WallpaperLight.exe", store);
                Assert(!service.Enabled);
                service.SetEnabled(false);
                Assert(store.Writes == 0);
                service.SetEnabled(true);
                Assert(service.Enabled && store.Value == "\"C:\\Обои Light\\WallpaperLight.exe\"");
                service.SetEnabled(true);
                Assert(store.Writes == 1);
                service.SetEnabled(false);
                Assert(store.Value is null);
                store.Value = "unrelated command";
                Throws<InvalidOperationException>(() => service.SetEnabled(true));
                service.SetEnabled(false);
                Assert(store.Value == "unrelated command");
                store.Value = "\"C:\\old\\WallpaperLight.exe\"";
                service.SetEnabled(true);
                Assert(service.Enabled);
            });
            Check("Second instance wakes first and releases ownership", () =>
            {
                string scope = "test-" + Guid.NewGuid().ToString("N");
                using (var first = new SingleInstanceService(scope))
                {
                    Assert(first.IsPrimary);
                    using var signaled = new ManualResetEventSlim();
                    first.Listen(signaled.Set);
                    Task.Run(() =>
                    {
                        using var second = new SingleInstanceService(scope);
                        Assert(!second.IsPrimary);
                        second.Signal();
                    }).GetAwaiter().GetResult();
                    Assert(signaled.Wait(TimeSpan.FromSeconds(5)));
                }
                using var replacement = new SingleInstanceService(scope);
                Assert(replacement.IsPrimary);
            });
            Check("Hidden startup, close, reopen and explicit exit", () =>
            {
                var service = new SettingsService(Path.Combine(temporary, "lifecycle"));
                service.Save(new AppSettings { StartMinimized = true });
                Assert(service.Load().Settings.StartMinimized);
                using var form = new MainForm(new FakeWallpaper(), service, service.Load());
                form.Start();
                Assert(!form.Visible && !form.IsDisposed);
                form.ShowFromTray();
                Assert(form.Visible);
                form.Close();
                Assert(!form.Visible && !form.IsDisposed);
                form.ShowFromTray();
                Assert(form.Visible);
                form.ExitApplication();
                Assert(form.IsDisposed);
            });
            Console.WriteLine($"{count} unit checks passed (no wallpaper or registry changes).");
        }
        finally
        {
            // This unique temporary tree is owned exclusively by this test run.
            Directory.Delete(temporary, recursive: true);
        }
    }

    private static void Check(string name, Action test)
    {
        test();
        count++;
        Console.WriteLine("PASS: " + name);
    }
    private static void Assert(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Unit assertion failed.");
    }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }

    private sealed class FakeStartupStore : IStartupStore
    {
        public string? Value;
        public int Writes;
        public string? Read() => Value;
        public void Write(string command) { Value = command; Writes++; }
        public void Delete() => Value = null;
    }

    private sealed class FakeWallpaper : IWallpaperService
    {
        public Dictionary<string, string> Current { get; } = new() { ["one"] = "", ["two"] = "" };
        public HashSet<string> Rejected { get; } = new();
        public int Calls { get; private set; }
        public MonitorInfo Monitor(string id = "one", bool portrait = false) => new(id,
            portrait ? new Rectangle(0, 0, 1080, 1920) : new Rectangle(0, 0, 1920, 1080), Current[id]);
        public IReadOnlyList<MonitorInfo> GetMonitors() => new[] { Monitor(), Monitor("two") };
        public void SetWallpaper(string monitorId, string path)
        {
            Calls++;
            if (Rejected.Contains(path)) throw new InvalidDataException("Broken test image");
            Current[monitorId] = path;
        }
    }
}
