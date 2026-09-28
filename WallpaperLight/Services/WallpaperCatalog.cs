namespace WallpaperLight.Services;

internal sealed class WallpaperCatalog
{
    private readonly Dictionary<string, IReadOnlyList<string>> cache = new(StringComparer.OrdinalIgnoreCase);

    public static bool IsSupported(string path) => Path.GetExtension(path).ToLowerInvariant()
        is ".jpg" or ".jpeg" or ".png" or ".bmp";

    // Called on the UI thread. Only directory enumeration runs on the pool;
    // the continuation and all COM calls stay on the original STA thread.
    public async Task<IReadOnlyList<string>> GetAsync(string folder, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folder)) return Array.Empty<string>();
        string fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        if (cache.TryGetValue(fullPath, out var existing)) return existing;
        var paths = await Task.Run(() => Scan(fullPath, cancellationToken), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        cache[fullPath] = paths;
        return paths;
    }

    public void Refresh() => cache.Clear();

    private static IReadOnlyList<string> Scan(string folder, CancellationToken token)
    {
        var files = new List<string>();
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = false, IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
            ReturnSpecialDirectories = false
        };
        foreach (string path in Directory.EnumerateFiles(folder, "*", options))
        {
            token.ThrowIfCancellationRequested();
            if (IsSupported(path)) files.Add(path);
        }
        files.Sort(StringComparer.OrdinalIgnoreCase);
        return files;
    }
}
