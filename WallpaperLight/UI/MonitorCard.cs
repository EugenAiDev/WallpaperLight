using WallpaperLight.Models;
using WallpaperLight.Services;

namespace WallpaperLight.UI;

internal sealed class MonitorCard : UserControl
{
    private readonly ToolTip details = new();
    public MonitorCard(MonitorInfo monitor, int number, AppSettings settings, bool canGoBack, bool readOnly,
        Action<MonitorInfo, WallpaperCommand> command, Action<MonitorInfo, PlaybackMode> modeChanged)
    {
        AutoScaleMode = AutoScaleMode.Inherit;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Dock = DockStyle.Top;
        Margin = new Padding(0, 0, 0, 12);
        var group = new Panel
        {
            AutoSize = true, Tag = "surface",
            AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, Padding = new Padding(18)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1,
            Padding = new Padding(0, 6, 0, 0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var title = Label($"{Texts.Get("Monitor")} {number}" + (monitor.Name.Length > 0 ? " · " + monitor.Name : ""));
        title.Font = Theme.CardFont;
        layout.Controls.Add(title);
        layout.Controls.Add(Label($"{monitor.Bounds.Width} × {monitor.Bounds.Height}  ·  " +
            Texts.Get(monitor.IsPortrait ? "Portrait" : "Landscape")));
        layout.Controls.Add(Label((monitor.IsPrimary ? Texts.Get("PrimaryMonitor") + " · " : "") +
            monitor.Connection + $" · {Texts.Get("Position")}: {monitor.Bounds.X}, {monitor.Bounds.Y}"));
        string folder = settings.Folders.For(monitor.IsPortrait);
        var folderField = ReadOnlyField(folder.Length == 0 ? Texts.Get("FolderNotSet") : Texts.Get("SourceFolderShort") + ": " + folder);
        folderField.Tag = "muted";
        details.SetToolTip(folderField, folder);
        layout.Controls.Add(folderField);
        var imageField = ReadOnlyField(Texts.Get("CurrentWallpaper") + ": " + (string.IsNullOrEmpty(monitor.WallpaperPath)
            ? Texts.Get("NoWallpaper") : Path.GetFileName(monitor.WallpaperPath)));
        details.SetToolTip(imageField, monitor.WallpaperPath);
        layout.Controls.Add(imageField);
        var modeRow = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Dock = DockStyle.Top, Margin = Padding.Empty };
        modeRow.Controls.Add(Label(Texts.Get("SelectionMode")));
        var modes = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210, Enabled = !readOnly };
        modes.Items.AddRange(new object[] { Texts.Get("Sequential"), Texts.Get("RandomMode") });
        modes.SelectedIndex = settings.ModeFor(monitor.DeviceId) == PlaybackMode.Sequential ? 0 : 1;
        modes.SelectedIndexChanged += (_, _) => modeChanged(monitor,
            modes.SelectedIndex == 0 ? PlaybackMode.Sequential : PlaybackMode.Random);
        modeRow.Controls.Add(modes);
        layout.Controls.Add(modeRow);
        var buttons = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 6, 0, 0), ColumnCount = 3, RowCount = 2
        };
        for (int i = 0; i < 3; i++) buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
        buttons.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        buttons.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        foreach (var (key, action) in new[]
        {
            ("Previous", WallpaperCommand.Previous), ("Next", WallpaperCommand.Next),
            ("Random", WallpaperCommand.Random), ("ChooseWallpaper", WallpaperCommand.Choose),
            ("OpenFolder", WallpaperCommand.OpenFolder)
        })
        {
            var button = new ThemedButton
            {
                Text = Texts.Get(key), AutoSize = false, Dock = DockStyle.Fill, Padding = new Padding(4, 0, 4, 0),
                Margin = new Padding(0, 6, 6, 0),
                Enabled = action == WallpaperCommand.Previous ? canGoBack :
                    action == WallpaperCommand.Choose || folder.Length > 0
            };
            button.Click += (_, _) => command(monitor, action);
            buttons.Controls.Add(button);
        }
        layout.Controls.Add(buttons);
        group.Controls.Add(layout);
        Controls.Add(group);
        AccessibleDescription = monitor.DeviceId;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) details.Dispose();
        base.Dispose(disposing);
    }

    private static Label Label(string text) => new()
    {
        Text = text, AutoSize = true, Margin = new Padding(0, 4, 8, 4)
    };

    private static TextBox ReadOnlyField(string text) => new()
    {
        Text = text, ReadOnly = true, Dock = DockStyle.Top,
        Margin = new Padding(0, 0, 0, 4), AccessibleName = text
    };
}
