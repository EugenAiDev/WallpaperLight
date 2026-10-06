using WallpaperLight.Models;

namespace WallpaperLight.UI;

internal sealed class SettingsForm : Form
{
    private readonly AppSettings original;
    private readonly TextBox landscape = new();
    private readonly TextBox portrait = new();
    private readonly ComboBox language = CreateSelector();
    private readonly ComboBox theme = CreateSelector();
    private readonly ComboBox accent = CreateSelector();
    private readonly ComboBox interval = CreateSelector();
    private readonly NumericUpDown minutes = new() { Minimum = 1, Maximum = 1440, Width = 90, ThousandsSeparator = false };
    private readonly CheckBox automatic = new() { AutoSize = true };
    private readonly CheckBox startWindows = new() { AutoSize = true };
    private readonly CheckBox startMinimized = new() { AutoSize = true };
    public bool StartWithWindows => startWindows.Checked;
    public AppSettings Updated => original with
    {
        Language = new[] { "system", "ru", "en" }[language.SelectedIndex],
        Theme = new[] { "system", "light", "dark" }[theme.SelectedIndex],
        Accent = new[] { "system", "blue", "teal", "violet" }[accent.SelectedIndex],
        StartMinimized = startMinimized.Checked,
        Slideshow = new() { Enabled = automatic.Checked, Mode = (IntervalMode)interval.SelectedIndex, CustomMinutes = (int)minutes.Value },
        Folders = new() { Landscape = landscape.Text.Trim(), Portrait = portrait.Text.Trim() }
    };

    public SettingsForm(AppSettings settings, bool startupEnabled, string? startupError = null)
    {
        original = settings;
        Text = Texts.Get("Settings");
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        ClientSize = new Size(700, 610);
        MinimumSize = new Size(610, 500);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 2 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var grid = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2 };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66));
        void Row(string key, Control control)
        {
            int row = grid.RowCount++;
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.Controls.Add(new Label { Text = Texts.Get(key), AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, 10, 16, 8) }, 0, row);
            control.Margin = new Padding(0, 6, 0, 6);
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            control.AccessibleName = Texts.Get(key);
            grid.Controls.Add(control, 1, row);
        }
        void Wide(string key)
        {
            int row = grid.RowCount++;
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var label = new Label { Text = Texts.Get(key), AutoSize = true, Dock = DockStyle.Top, Tag = "muted", Margin = new Padding(0, 10, 0, 16) };
            grid.Controls.Add(label, 0, row); grid.SetColumnSpan(label, 2);
        }
        Wide("SharedFoldersHint");
        Row("LandscapeFolder", FolderRow(landscape, settings.Folders.Landscape));
        Row("PortraitFolder", FolderRow(portrait, settings.Folders.Portrait));
        automatic.Text = Texts.Get("SlideshowEnabled"); automatic.Checked = settings.Slideshow.Enabled;
        Row("Slideshow", automatic);
        interval.Items.AddRange(new object[] { Texts.Get("Interval30"), Texts.Get("Interval60"), Texts.Get("IntervalCustom"), Texts.Get("IntervalRandom") });
        interval.SelectedIndex = (int)settings.Slideshow.Mode;
        Row("Interval", interval);
        minutes.Value = settings.Slideshow.CustomMinutes;
        Row("CustomMinutes", minutes);
        void UpdateTimerControls() { interval.Enabled = automatic.Checked; minutes.Enabled = automatic.Checked && interval.SelectedIndex == (int)IntervalMode.Custom; }
        automatic.CheckedChanged += (_, _) => UpdateTimerControls();
        interval.SelectedIndexChanged += (_, _) => UpdateTimerControls();
        UpdateTimerControls();
        Wide("SleepTimerHint");
        language.Items.AddRange(new object[] { Texts.Get("SystemLanguage"), "Русский", "English" });
        language.SelectedIndex = Array.IndexOf(new[] { "system", "ru", "en" }, settings.Language);
        Row("Language", language);
        theme.Items.AddRange(new object[] { Texts.Get("SystemLanguage"), Texts.Get("LightTheme"), Texts.Get("DarkTheme") });
        theme.SelectedIndex = Array.IndexOf(new[] { "system", "light", "dark" }, settings.Theme);
        Row("Theme", theme);
        accent.Items.AddRange(new object[] { Texts.Get("SystemLanguage"), Texts.Get("AccentBlue"), Texts.Get("AccentTeal"), Texts.Get("AccentViolet") });
        accent.SelectedIndex = Array.IndexOf(new[] { "system", "blue", "teal", "violet" }, settings.Accent);
        Row("Accent", accent);
        theme.SelectedIndexChanged += (_, _) => ApplyTheme();
        accent.SelectedIndexChanged += (_, _) => ApplyTheme();
        startWindows.Text = Texts.Get("StartWithWindows"); startWindows.Checked = startupEnabled; startWindows.Enabled = startupError is null;
        Row("Startup", startWindows);
        startMinimized.Text = Texts.Get("StartMinimized"); startMinimized.Checked = settings.StartMinimized;
        Row("Window", startMinimized);
        Wide("TrayHint");
        if (startupError is not null) Row("AutostartFailed", new Label { Text = startupError, AutoSize = true });
        scroll.Controls.Add(grid); root.Controls.Add(scroll, 0, 0);
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0, 16, 0, 0) };
        var save = new ThemedButton { Text = Texts.Get("Save"), AutoSize = true, Padding = new Padding(16,6,16,6), DialogResult = DialogResult.OK, Tag = "accent" };
        var cancel = new ThemedButton { Text = Texts.Get("Cancel"), AutoSize = true, Padding = new Padding(16,6,16,6), DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(save); buttons.Controls.Add(cancel); root.Controls.Add(buttons, 0, 1);
        AcceptButton = save; CancelButton = cancel; Controls.Add(root);
        ApplyTheme(); HandleCreated += (_, _) => ApplyTheme();
        FormClosing += (_, e) =>
        {
            if (DialogResult != DialogResult.OK) return;
            foreach (string folder in new[] { landscape.Text.Trim(), portrait.Text.Trim() })
            {
                if (folder.Length == 0 || (Path.IsPathFullyQualified(folder) && Directory.Exists(folder))) continue;
                MessageBox.Show(this, Texts.Get("InvalidFolder"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                e.Cancel = true; break;
            }
        };
    }
    private void ApplyTheme()
    {
        if (theme.SelectedIndex >= 0 && accent.SelectedIndex >= 0) UI.Theme.Apply(this, Updated.Theme, Updated.Accent);
    }
    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (message.Msg is 0x001A or 0x031A) ApplyTheme();
    }
    private static ComboBox CreateSelector() => new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top };
    private Control FolderRow(TextBox field, string path)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2 };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        field.Text = path; field.Dock = DockStyle.Fill;
        var browse = new ThemedButton { Text = Texts.Get("Browse"), AutoSize = true };
        browse.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { Description = Texts.Get("SelectFolder"), UseDescriptionForTitle = true, SelectedPath = Directory.Exists(field.Text) ? field.Text : "" };
            if (dialog.ShowDialog(this) == DialogResult.OK) field.Text = dialog.SelectedPath;
        };
        row.Controls.Add(field, 0, 0); row.Controls.Add(browse, 1, 0); return row;
    }
}
