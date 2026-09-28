using WallpaperLight.Models;

namespace WallpaperLight.UI;

internal sealed class SettingsForm : Form
{
    private readonly AppSettings original;
    private readonly TextBox landscape = new();
    private readonly TextBox portrait = new();
    private readonly ComboBox language = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox startWindows = new() { AutoSize = true };
    private readonly CheckBox startMinimized = new() { AutoSize = true };
    private readonly ComboBox theme = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top };
    public bool StartWithWindows => startWindows.Checked;
    public AppSettings Updated => original with
    {
        Language = new[] { "system", "ru", "en" }[language.SelectedIndex],
        StartMinimized = startMinimized.Checked,
        Theme = new[] { "system", "light", "dark" }[theme.SelectedIndex],
        Folders = new WallpaperFolders { Landscape = landscape.Text.Trim(), Portrait = portrait.Text.Trim() }
    };

    public SettingsForm(AppSettings settings, bool startupEnabled, string? startupError = null)
    {
        original = settings;
        Text = Texts.Get("Settings");
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        ClientSize = new Size(640, 550);
        MinimumSize = new Size(500, 590);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 13, AutoScroll = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 12; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(Caption("SharedFoldersHint"));
        layout.Controls.Add(Caption("LandscapeFolder"));
        layout.Controls.Add(FolderRow(landscape, settings.Folders.Landscape));
        layout.Controls.Add(Caption("PortraitFolder"));
        layout.Controls.Add(FolderRow(portrait, settings.Folders.Portrait));
        layout.Controls.Add(Caption("Language"));
        language.Items.AddRange(new object[] { Texts.Get("SystemLanguage"), "Русский", "English" });
        language.SelectedIndex = Array.IndexOf(new[] { "system", "ru", "en" }, settings.Language);
        language.Dock = DockStyle.Top;
        layout.Controls.Add(language);
        layout.Controls.Add(Caption("Theme"));
        theme.Items.AddRange(new object[] { Texts.Get("SystemLanguage"), Texts.Get("LightTheme"), Texts.Get("DarkTheme") });
        theme.SelectedIndex = Array.IndexOf(new[] { "system", "light", "dark" }, settings.Theme);
        layout.Controls.Add(theme);
        theme.SelectedIndexChanged += (_, _) => UI.Theme.Apply(this, Updated.Theme);
        startWindows.Text = Texts.Get("StartWithWindows");
        startWindows.Checked = startupEnabled;
        startWindows.Enabled = startupError is null;
        layout.Controls.Add(startWindows);
        startMinimized.Text = Texts.Get("StartMinimized");
        startMinimized.Checked = settings.StartMinimized;
        layout.Controls.Add(startMinimized);
        var hint = Caption("TrayHint");
        if (startupError is not null) hint.Text += Environment.NewLine + Texts.Get("AutostartFailed") + " " + startupError;
        layout.Controls.Add(hint);
        var buttons = new FlowLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0, 16, 0, 0)
        };
        var save = new ThemedButton { Text = Texts.Get("Save"), AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new ThemedButton { Text = Texts.Get("Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons);
        AcceptButton = save;
        CancelButton = cancel;
        Controls.Add(layout);
        UI.Theme.Apply(this, settings.Theme);
        HandleCreated += (_, _) => UI.Theme.Apply(this, Updated.Theme);
        FormClosing += (_, e) =>
        {
            if (DialogResult != DialogResult.OK) return;
            foreach (string folder in new[] { landscape.Text.Trim(), portrait.Text.Trim() })
            {
                if (folder.Length == 0 || (Path.IsPathFullyQualified(folder) && Directory.Exists(folder))) continue;
                MessageBox.Show(this, Texts.Get("InvalidFolder"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                e.Cancel = true;
                break;
            }
        };
    }

    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (message.Msg is 0x001A or 0x031A && theme.SelectedIndex >= 0) UI.Theme.Apply(this, Updated.Theme);
    }

    private static Label Caption(string key) => new()
    {
        Text = Texts.Get(key), AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, 8, 0, 4)
    };

    private Control FolderRow(TextBox field, string path)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2 };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        field.Text = path;
        field.Dock = DockStyle.Fill;
        var browse = new ThemedButton { Text = Texts.Get("Browse"), AutoSize = true };
        browse.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = Texts.Get("SelectFolder"), UseDescriptionForTitle = true,
                SelectedPath = Directory.Exists(field.Text) ? field.Text : ""
            };
            if (dialog.ShowDialog(this) == DialogResult.OK) field.Text = dialog.SelectedPath;
        };
        row.Controls.Add(field, 0, 0);
        row.Controls.Add(browse, 1, 0);
        return row;
    }
}
