using System.Globalization;
using System.Resources;

namespace WallpaperLight.UI;

internal static class Texts
{
    private static readonly CultureInfo SystemCulture = CultureInfo.CurrentUICulture;
    private static readonly ResourceManager Resources = new("WallpaperLight.Resources.Strings", typeof(Texts).Assembly);
    public static string Get(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    public static void SetLanguage(string language)
    {
        CultureInfo.CurrentUICulture = language == "system" ? SystemCulture : CultureInfo.GetCultureInfo(language);
    }
}
