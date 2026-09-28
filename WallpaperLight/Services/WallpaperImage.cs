using WallpaperLight.UI;

namespace WallpaperLight.Services;

internal static class WallpaperImage
{
    public static void Validate(string path)
    {
        if (!WallpaperCatalog.IsSupported(path)) throw new NotSupportedException(Texts.Get("UnsupportedImage"));
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            // Validate only a selected candidate, never every file during scanning.
            using var image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
        }
        catch (Exception error) when (error is ArgumentException or OutOfMemoryException)
        {
            // GDI+ also reports OutOfMemory for malformed image data.
            throw new InvalidDataException(Texts.Get("InvalidImage"), error);
        }
    }
}
