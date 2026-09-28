namespace WallpaperLight.UI;

internal sealed class ThemedButton : Button
{
    protected override void OnPaint(PaintEventArgs e)
    {
        if (Enabled || SystemInformation.HighContrast || BackColor.GetBrightness() > 0.4f)
        { base.OnPaint(e); return; }
        e.Graphics.Clear(BackColor);
        ControlPaint.DrawBorder(e.Graphics, ClientRectangle, FlatAppearance.BorderColor, ButtonBorderStyle.Solid);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Color.FromArgb(150,160,176),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
    }
}
