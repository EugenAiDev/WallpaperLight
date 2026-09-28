using System.Runtime.InteropServices;
using WallpaperLight.Models;

namespace WallpaperLight.Services;

internal enum WallpaperCommand { Previous, Next, Random, Choose, OpenFolder }
internal sealed record ChangeResult(string MessageKey, string? Detail = null);

internal sealed class WallpaperNavigator(IWallpaperService wallpaper, Random? random = null)
{
    private readonly Random random = random ?? Random.Shared;
    private readonly Dictionary<(string Id, bool Portrait), SelectionSession> sessions = new();

    public void ResetOrientation(bool portrait)
    {
        foreach (var key in sessions.Keys.Where(k => k.Portrait == portrait).ToArray()) sessions.Remove(key);
    }

    public void RefreshCandidates()
    {
        foreach (var session in sessions.Values) session.RefreshCandidates();
    }

    public bool CanGoBack(MonitorInfo monitor) => Session(monitor).HistoryIndex > 0;

    public void Choose(MonitorInfo monitor, string path)
    {
        string fullPath = Path.GetFullPath(path);
        wallpaper.SetWallpaper(monitor.DeviceId, fullPath);
        Session(monitor).Record(fullPath);
    }

    public ChangeResult Change(MonitorInfo monitor, IReadOnlyList<string> files, PlaybackMode mode,
        WallpaperCommand command)
    {
        var session = Session(monitor);
        if (command == WallpaperCommand.Previous) return Previous(monitor, session);
        if (files.Count == 0) return new("NoImages");
        bool useRandom = command == WallpaperCommand.Random || mode == PlaybackMode.Random;
        string current = monitor.WallpaperPath;
        var available = files.Where(p => !session.Rejected.Contains(p)).ToList();
        if (available.Count == 0) return new("NoUsableImages");
        if (available.Count == 1 && Same(available[0], current)) return new("AlreadyCurrent");

        IEnumerable<string> candidates;
        if (useRandom)
        {
            var availableSet = new HashSet<string>(available, StringComparer.OrdinalIgnoreCase);
            session.RandomQueue.RemoveAll(p => !availableSet.Contains(p));
            if (session.RandomQueue.Count == 0 || session.RandomQueue.All(p => Same(p, current)))
            {
                session.RandomQueue.Clear();
                session.RandomQueue.AddRange(available);
                for (int i = session.RandomQueue.Count - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    (session.RandomQueue[i], session.RandomQueue[j]) = (session.RandomQueue[j], session.RandomQueue[i]);
                }
                if (session.RandomQueue.Count > 1 && Same(session.RandomQueue[0], current))
                {
                    int swap = random.Next(1, session.RandomQueue.Count);
                    (session.RandomQueue[0], session.RandomQueue[swap]) = (session.RandomQueue[swap], session.RandomQueue[0]);
                }
            }
            candidates = session.RandomQueue.ToArray();
        }
        else
        {
            int start = available.FindIndex(p => Same(p, current));
            candidates = Enumerable.Range(1, available.Count).Select(offset => available[(start + offset) % available.Count]);
        }

        string? lastError = null;
        foreach (string candidate in candidates)
        {
            if (Same(candidate, current)) continue;
            session.RandomQueue.RemoveAll(p => Same(p, candidate));
            try
            {
                wallpaper.SetWallpaper(monitor.DeviceId, candidate);
                session.Record(candidate);
                return new("WallpaperApplied");
            }
            catch (Exception error) when (IsFileFailure(error))
            {
                session.Rejected.Add(candidate);
                lastError = error.Message;
            }
        }
        return new("NoUsableImages", lastError);
    }

    private ChangeResult Previous(MonitorInfo monitor, SelectionSession session)
    {
        for (int index = session.HistoryIndex - 1; index >= 0; index--)
        {
            try
            {
                wallpaper.SetWallpaper(monitor.DeviceId, session.History[index]);
                session.HistoryIndex = index;
                return new("WallpaperApplied");
            }
            catch (Exception error) when (IsFileFailure(error))
            {
                session.History.RemoveAt(index);
                session.HistoryIndex--;
            }
        }
        return new("NoPrevious");
    }

    private SelectionSession Session(MonitorInfo monitor)
    {
        var key = (monitor.DeviceId.ToUpperInvariant(), monitor.IsPortrait);
        if (!sessions.TryGetValue(key, out var session)) sessions[key] = session = new();
        return session;
    }

    private static bool Same(string first, string second) => StringComparer.OrdinalIgnoreCase.Equals(first, second);
    private static bool IsFileFailure(Exception error) =>
        error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException ||
        error is COMException com && com.HResult is unchecked((int)0x80070002) or unchecked((int)0x80070003) or unchecked((int)0x8007000D);

    private sealed class SelectionSession
    {
        public List<string> RandomQueue { get; } = new();
        public HashSet<string> Rejected { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> History { get; } = new();
        public int HistoryIndex { get; set; } = -1;

        public void Record(string path)
        {
            if (HistoryIndex + 1 < History.Count) History.RemoveRange(HistoryIndex + 1, History.Count - HistoryIndex - 1);
            if (History.Count == 0 || !Same(History[^1], path)) History.Add(path);
            if (History.Count > 50) History.RemoveAt(0);
            HistoryIndex = History.Count - 1;
            RandomQueue.RemoveAll(p => Same(p, path));
        }

        public void RefreshCandidates()
        {
            RandomQueue.Clear();
            Rejected.Clear();
        }
    }
}
