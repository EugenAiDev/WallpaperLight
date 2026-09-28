using System.Diagnostics;
using System.Runtime.InteropServices;
using WallpaperLight.Models;
using WallpaperLight.Services;

namespace WallpaperLight.UI;

internal sealed class MainForm : Form
{
    private readonly IWallpaperService wallpaper;
    private readonly SettingsService settingsService;
    private readonly SettingsLoadResult loadResult;
    private readonly WallpaperCatalog catalog = new();
    private readonly WallpaperNavigator navigator;
    private readonly CancellationTokenSource lifetime = new();
    private readonly CancellationToken shutdown;
    private readonly Dictionary<string, bool> orientations = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Windows.Forms.Timer displayChangeTimer = new() { Interval = 500 };
    private AppSettings settings;
    private IReadOnlyList<MonitorInfo> monitors = Array.Empty<MonitorInfo>();
    private TableLayoutPanel root = null!;
    private TableLayoutPanel cards = null!;
    private Label status = null!;
    private bool busy;
    private bool displayRefreshPending;
    private readonly StartupService startup = new(Path.Combine(AppContext.BaseDirectory, "WallpaperLight.exe"));
    private readonly NotifyIcon tray;
    private readonly ContextMenuStrip trayMenu = new();
    private bool exiting;
    private readonly Icon applicationIcon = new(typeof(MainForm).Assembly.GetManifestResourceStream("WallpaperLight.Assets.WallpaperLight.ico")!);

    public MainForm(IWallpaperService wallpaper, SettingsService settingsService, SettingsLoadResult loadResult)
    {
        this.wallpaper = wallpaper;
        this.settingsService = settingsService;
        this.loadResult = loadResult;
        shutdown = lifetime.Token;
        settings = loadResult.Settings;
        navigator = new WallpaperNavigator(wallpaper);
        Text = "Wallpaper Light";
        Icon = applicationIcon;
        HandleCreated += (_, _) => Theme.Apply(this, settings.Theme);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        ClientSize = new Size(800, 780);
        MinimumSize = new Size(480, 400);
        StartPosition = FormStartPosition.CenterScreen;
        BuildUi();
        tray = new NotifyIcon { Text = "Wallpaper Light", Icon = applicationIcon, ContextMenuStrip = trayMenu, Visible = true };
        tray.DoubleClick += (_, _) => ShowFromTray();
        trayMenu.Opening += (_, _) => BuildTrayMenu();
        displayChangeTimer.Tick += (_, _) =>
        {
            displayChangeTimer.Stop();
            if (busy) displayRefreshPending = true;
            else Run(() => RefreshMonitorsAsync(reactToOrientation: true));
        };
    }

    public void Start()
    {
        _ = Handle;
        BeginInvoke(() => Run(() => RefreshMonitorsAsync(reactToOrientation: false)));
        if (!settings.StartMinimized) Show();
    }

    public void QueueShowFromTray()
    {
        try { BeginInvoke(ShowFromTray); }
        catch (InvalidOperationException error) { Trace.TraceInformation(error.Message); }
    }

    internal void ShowFromTray()
    {
        if (IsDisposed || exiting) return;
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
        foreach (var owned in OwnedForms) if (owned.Visible) owned.Activate();
    }

    internal void ExitApplication() { exiting = true; Close(); }

    private void BuildTrayMenu()
    {
        while (trayMenu.Items.Count > 0) trayMenu.Items[0].Dispose();
        trayMenu.Items.Add(Texts.Get("OpenApplication"), null, (_, _) => ShowFromTray());
        bool available = !busy && !OwnedForms.Any(f => f.Visible);
        var all = trayMenu.Items.Add(Texts.Get("ChangeAll"), null, (_, _) => Run(ChangeAllAsync));
        all.Enabled = available && monitors.Count > 0;
        for (int i = 0; i < monitors.Count; i++)
        {
            var monitor = monitors[i];
            var menu = new ToolStripMenuItem($"{Texts.Get("Monitor")} {i + 1}") { Enabled = available };
            foreach (var command in new[] { WallpaperCommand.Previous, WallpaperCommand.Next, WallpaperCommand.Random })
            {
                var item = menu.DropDownItems.Add(Texts.Get(command.ToString()), null, (_, _) => Execute(monitor, command));
                item.Enabled = command == WallpaperCommand.Previous ? navigator.CanGoBack(monitor) : settings.Folders.For(monitor.IsPortrait).Length > 0;
            }
            trayMenu.Items.Add(menu);
        }
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add(Texts.Get("ExitApplication"), null, (_, _) => ExitApplication());
        Theme.ApplyMenu(trayMenu, settings.Theme);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && !exiting) { e.Cancel = true; Hide(); }
        base.OnFormClosing(e);
    }

    private void BuildUi()
    {
        SuspendLayout();
        try
        {
            root?.Dispose();
            root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(16) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 0, 0, 8) };
            toolbar.Controls.Add(Button("Settings", OpenSettings, !settingsService.IsReadOnly));
            toolbar.Controls.Add(Button("RefreshAll", () => Run(async () =>
            {
                catalog.Refresh();
                navigator.RefreshCandidates();
                return await RefreshMonitorsAsync(reactToOrientation: true);
            })));
            toolbar.Controls.Add(Button("ChangeAll", () => Run(ChangeAllAsync)));
            root.Controls.Add(toolbar, 0, 0);
            var warning = new Label
            {
                AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 8),
                Text = loadResult.WarningKey is null ? "" : Texts.Get(loadResult.WarningKey) + " " + loadResult.Detail,
                Visible = loadResult.WarningKey is not null
            };
            root.Controls.Add(warning, 0, 1);
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
            cards = new TableLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1
            };
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            scroll.Controls.Add(cards);
            root.Controls.Add(scroll, 0, 2);
            status = new Label { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 0) };
            root.Controls.Add(status, 0, 3);
            Controls.Add(root);
            RenderCards();
        }
        finally { ResumeLayout(true); }
    }

    private static Button Button(string key, Action action, bool enabled = true)
    {
        var button = new ThemedButton { Text = Texts.Get(key), AutoSize = true, Padding = new Padding(6, 4, 6, 4), Enabled = enabled };
        button.Click += (_, _) => action();
        return button;
    }

    private void RenderCards()
    {
        cards.SuspendLayout();
        try
        {
            while (cards.Controls.Count > 0) cards.Controls[0].Dispose();
            cards.RowStyles.Clear();
            cards.RowCount = monitors.Count;
            for (int i = 0; i < monitors.Count; i++)
            {
                cards.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                cards.Controls.Add(new MonitorCard(monitors[i], i + 1, settings, navigator.CanGoBack(monitors[i]),
                    settingsService.IsReadOnly, Execute, ChangeMode), 0, i);
            }
        }
        finally { cards.ResumeLayout(true); }
        Theme.Apply(this, settings.Theme);
    }

    private async Task<string> RefreshMonitorsAsync(bool reactToOrientation)
    {
        monitors = wallpaper.GetMonitors();
        var outcomes = new List<string>();
        if (reactToOrientation)
        {
            foreach (var monitor in monitors)
            {
                if (orientations.TryGetValue(monitor.DeviceId, out bool wasPortrait) && wasPortrait != monitor.IsPortrait)
                {
                    try { outcomes.Add(Describe(await ChangeAsync(monitor, WallpaperCommand.Next))); }
                    catch (Exception error) when (IsExpectedError(error)) { outcomes.Add(error.Message); }
                }
            }
            shutdown.ThrowIfCancellationRequested();
            monitors = wallpaper.GetMonitors();
        }
        orientations.Clear();
        foreach (var monitor in monitors) orientations[monitor.DeviceId] = monitor.IsPortrait;
        RenderCards();
        return outcomes.Count > 0 ? string.Join(" · ", outcomes) :
            monitors.Count == 0 ? Texts.Get("NoMonitors") : string.Format(Texts.Get("MonitorCount"), monitors.Count);
    }

    private async Task<ChangeResult> ChangeAsync(MonitorInfo monitor, WallpaperCommand command)
    {
        IReadOnlyList<string> files = Array.Empty<string>();
        if (command != WallpaperCommand.Previous)
        {
            string folder = settings.Folders.For(monitor.IsPortrait);
            if (folder.Length == 0) return new("FolderNotSet");
            files = await catalog.GetAsync(folder, shutdown);
        }
        shutdown.ThrowIfCancellationRequested();
        return navigator.Change(monitor, files, settings.ModeFor(monitor.DeviceId), command);
    }

    private void Execute(MonitorInfo monitor, WallpaperCommand command)
    {
        if (busy) return;
        if (command == WallpaperCommand.Choose)
        {
            using var dialog = new OpenFileDialog
            {
                Title = Texts.Get("ChooseWallpaper"), Filter = Texts.Get("ImageFilter"),
                CheckFileExists = true, Multiselect = false, RestoreDirectory = true
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            Run(() =>
            {
                navigator.Choose(monitor, dialog.FileName);
                return Task.FromResult(Texts.Get("WallpaperApplied"));
            });
        }
        else if (command == WallpaperCommand.OpenFolder)
        {
            Run(() =>
            {
                string folder = settings.Folders.For(monitor.IsPortrait);
                if (!Directory.Exists(folder)) throw new DirectoryNotFoundException(Texts.Get("InvalidFolder"));
                Process.Start(new ProcessStartInfo(Path.GetFullPath(folder)) { UseShellExecute = true });
                return Task.FromResult(Texts.Get("FolderOpened"));
            });
        }
        else Run(async () => Describe(await ChangeAsync(monitor, command)));
    }

    private async Task<string> ChangeAllAsync()
    {
        monitors = wallpaper.GetMonitors();
        var outcomes = new List<string>();
        for (int index = 0; index < monitors.Count; index++)
        {
            string result;
            try { result = Describe(await ChangeAsync(monitors[index], WallpaperCommand.Next)); }
            catch (Exception error) when (IsExpectedError(error)) { result = error.Message; }
            outcomes.Add($"{Texts.Get("Monitor")} {index + 1}: {result}");
        }
        return string.Join(Environment.NewLine, outcomes);
    }

    private void OpenSettings()
    {
        if (busy) return;
        bool startupEnabled = false;
        string? startupError = null;
        try { startupEnabled = startup.Enabled; }
        catch (Exception error) when (IsExpectedError(error)) { startupError = error.Message; }
        using var dialog = new SettingsForm(settings, startupEnabled, startupError);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        Run(() =>
        {
            var updated = dialog.Updated;
            settingsService.Save(updated);
            if (!StringComparer.OrdinalIgnoreCase.Equals(settings.Folders.Landscape, updated.Folders.Landscape))
                navigator.ResetOrientation(false);
            if (!StringComparer.OrdinalIgnoreCase.Equals(settings.Folders.Portrait, updated.Folders.Portrait))
                navigator.ResetOrientation(true);
            catalog.Refresh();
            settings = updated;
            Texts.SetLanguage(settings.Language);
            BuildUi();
            if (startupError is null && dialog.StartWithWindows != startupEnabled)
            {
                try { startup.SetEnabled(dialog.StartWithWindows); }
                catch (Exception error) when (IsExpectedError(error))
                { return Task.FromResult(Texts.Get("SettingsSaved") + " " + Texts.Get("AutostartFailed") + " " + error.Message); }
            }
            return Task.FromResult(Texts.Get("SettingsSaved"));
        });
    }

    private void ChangeMode(MonitorInfo monitor, PlaybackMode mode) => Run(() =>
    {
        var updated = settings with { Monitors = new(settings.Monitors, StringComparer.OrdinalIgnoreCase) };
        updated.Monitors[monitor.DeviceId] = new MonitorSettings { Mode = mode };
        settingsService.Save(updated);
        settings = updated;
        return Task.FromResult(Texts.Get("SettingsSaved"));
    });

    // Serializes user commands and display refreshes without blocking folder scans.
    private async void Run(Func<Task<string>> operation)
    {
        if (busy || IsDisposed) return;
        busy = true;
        root.Enabled = false;
        UseWaitCursor = true;
        string result;
        try { result = await operation(); }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { return; }
        catch (Exception error) when (IsExpectedError(error)) { result = Texts.Get("OperationFailed") + " " + error.Message; }
        finally
        {
            busy = false;
            if (!IsDisposed) { root.Enabled = true; UseWaitCursor = false; }
        }
        if (IsDisposed || shutdown.IsCancellationRequested) return;
        try { monitors = wallpaper.GetMonitors(); RenderCards(); }
        catch (Exception error) when (IsExpectedError(error)) { result += Environment.NewLine + error.Message; }
        status.Text = result;
        if (displayRefreshPending)
        {
            displayRefreshPending = false;
            displayChangeTimer.Start();
        }
    }

    private static string Describe(ChangeResult result) => Texts.Get(result.MessageKey) +
        (result.Detail is null ? "" : " " + result.Detail);
    private static bool IsExpectedError(Exception error) => error is COMException or IOException or InvalidDataException or
        UnauthorizedAccessException or ArgumentException or NotSupportedException or InvalidOperationException or
        System.ComponentModel.Win32Exception or System.Security.SecurityException;

    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (message.Msg is 0x001A or 0x031A && settings is not null) Theme.Apply(this, settings.Theme);
        if (message.Msg == 0x007E)
        {
            displayChangeTimer.Stop();
            displayChangeTimer.Start();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (!shutdown.IsCancellationRequested)
            {
                lifetime.Cancel();
                lifetime.Dispose();
            }
            displayChangeTimer.Dispose();
            tray.Visible = false;
            tray.Dispose();
            trayMenu.Dispose();
            applicationIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
