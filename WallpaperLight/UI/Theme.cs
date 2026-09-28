using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace WallpaperLight.UI;

internal static class Theme
{
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

    public static void Apply(Control control, string preference)
    {
        bool dark = IsDark(preference);
        Paint(control, dark, SystemInformation.HighContrast);
        if (control is Form form && form.IsHandleCreated)
        {
            int enabled = dark ? 1 : 0;
            DwmSetWindowAttribute(form.Handle, 20, ref enabled, sizeof(int));
        }
    }

    private static void Paint(Control control, bool dark, bool contrast)
    {
        Color background = dark ? Color.FromArgb(25, 29, 38) : SystemColors.Control;
        Color field = dark ? Color.FromArgb(36, 42, 54) : SystemColors.Window;
        Color foreground = dark ? Color.FromArgb(235, 239, 245) : SystemColors.ControlText;
        control.BackColor = control is TextBoxBase or ComboBox ? field : background;
        control.ForeColor = foreground;
        if (control is Button button)
        {
            button.FlatStyle = contrast ? FlatStyle.Standard : FlatStyle.Flat;
            button.UseVisualStyleBackColor = contrast;
            button.FlatAppearance.BorderColor = dark ? Color.FromArgb(75, 88, 110) : SystemColors.ControlDark;
            button.BackColor = dark ? field : SystemColors.Control;
        }
        if (control is ComboBox combo) combo.FlatStyle = contrast ? FlatStyle.Standard : FlatStyle.Flat;
        foreach (Control child in control.Controls) Paint(child, dark, contrast);
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
}
