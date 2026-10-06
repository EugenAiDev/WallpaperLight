using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace WallpaperLight.UI;

internal static class Theme
{
    public static readonly Font TitleFont = new("Segoe UI", 20, FontStyle.Regular);
    public static readonly Font CardFont = new("Segoe UI", 11, FontStyle.Bold);
    public static bool IsDark(string preference)
    {
        if (SystemInformation.HighContrast) return false;
        if (preference != "system") return preference == "dark";
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        { System.Diagnostics.Trace.TraceInformation(error.Message); return false; }
    }

    public static void Apply(Control control, string preference, string accent = "system")
    {
        bool dark = IsDark(preference);
        Paint(control, dark, SystemInformation.HighContrast, AccentColor(accent), false);
        if (control is Form form && form.IsHandleCreated)
        {
            int enabled = dark ? 1 : 0;
            DwmSetWindowAttribute(form.Handle, 20, ref enabled, sizeof(int));
        }
    }

    private static Color AccentColor(string value)
    {
        if (value == "blue") return Color.FromArgb(0, 103, 192);
        if (value == "teal") return Color.FromArgb(0, 120, 112);
        if (value == "violet") return Color.FromArgb(112, 65, 180);
        if (DwmGetColorizationColor(out uint color, out _) == 0)
            return Color.FromArgb((int)((color >> 16) & 255), (int)((color >> 8) & 255), (int)(color & 255));
        return Color.FromArgb(0, 103, 192);
    }

    private static void Paint(Control control, bool dark, bool contrast, Color accent, bool onSurface)
    {
        onSurface |= Equals(control.Tag, "surface");
        Color background = contrast ? SystemColors.Control : dark ? Color.FromArgb(onSurface ? 43 : 32, onSurface ? 43 : 32, onSurface ? 43 : 32) : onSurface ? Color.White : Color.FromArgb(243,243,243);
        Color field = dark ? Color.FromArgb(52,52,52) : SystemColors.Window;
        Color foreground = dark ? Color.FromArgb(242,242,242) : SystemColors.ControlText;
        control.BackColor = control is TextBoxBase or ComboBox or NumericUpDown ? field : background;
        control.ForeColor = foreground;
        if (Equals(control.Tag, "muted") && !contrast) control.ForeColor = dark ? Color.FromArgb(187,187,187) : Color.FromArgb(96,96,96);
        if (control is TextBoxBase text && text.ReadOnly) { text.BackColor = background; text.BorderStyle = BorderStyle.None; }
        if (control is Button button)
        {
            button.FlatStyle = contrast ? FlatStyle.Standard : FlatStyle.Flat;
            button.UseVisualStyleBackColor = contrast;
            button.FlatAppearance.BorderColor = dark ? Color.FromArgb(75,75,75) : Color.FromArgb(210,210,210);
            button.BackColor = dark ? field : Color.White;
            if (!contrast && Equals(button.Tag, "accent"))
            {
                button.BackColor = accent;
                button.ForeColor = accent.GetBrightness() > 0.6f ? Color.Black : Color.White;
                button.FlatAppearance.BorderColor = accent;
            }
        }
        if (control is ComboBox combo) combo.FlatStyle = contrast ? FlatStyle.Standard : FlatStyle.Flat;
        foreach (Control child in control.Controls) Paint(child, dark, contrast, accent, onSurface);
        control.Invalidate();
    }

    public static void ApplyMenu(ContextMenuStrip menu, string preference)
    {
        bool dark = IsDark(preference);
        menu.Renderer = dark ? new ToolStripProfessionalRenderer(new DarkColors()) : new ToolStripSystemRenderer();
        foreach (ToolStripItem item in menu.Items) ApplyItem(item, dark);
    }
    private static void ApplyItem(ToolStripItem item, bool dark)
    {
        item.ForeColor = dark ? Color.FromArgb(235,239,245) : SystemColors.MenuText;
        if (item is ToolStripMenuItem parent)
            foreach (ToolStripItem child in parent.DropDownItems) ApplyItem(child, dark);
    }
    private sealed class DarkColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.FromArgb(25,29,38);
        public override Color ImageMarginGradientBegin => ToolStripDropDownBackground;
        public override Color ImageMarginGradientMiddle => ToolStripDropDownBackground;
        public override Color ImageMarginGradientEnd => ToolStripDropDownBackground;
        public override Color MenuItemSelected => Color.FromArgb(54,68,89);
        public override Color MenuItemBorder => Color.FromArgb(91,156,215);
        public override Color MenuBorder => Color.FromArgb(75,88,110);
    }
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetColorizationColor(out uint color, [MarshalAs(UnmanagedType.Bool)] out bool opaque);
}
