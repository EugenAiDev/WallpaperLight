using System.Diagnostics;
using System.Security.Principal;

namespace WallpaperLight.Services;

internal sealed class SingleInstanceService : IDisposable
{
    private readonly Mutex mutex;
    private readonly EventWaitHandle wake;
    private RegisteredWaitHandle? listener;
    public bool IsPrimary { get; }

    public SingleInstanceService(string? scope = null)
    {
        using var identity = WindowsIdentity.GetCurrent();
        using var process = Process.GetCurrentProcess();
        string name = @"Local\WallpaperLight." + (scope ?? identity.User!.Value + "." + process.SessionId);
        mutex = new Mutex(false, name + ".Mutex");
        wake = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".Wake");
        try { IsPrimary = mutex.WaitOne(0); }
        catch (AbandonedMutexException) { IsPrimary = true; }
    }

    public void Signal() => wake.Set();
    public void Listen(Action action) => listener = ThreadPool.RegisterWaitForSingleObject(wake,
        (_, _) => action(), null, Timeout.Infinite, false);

    public void Dispose()
    {
        listener?.Unregister(null);
        wake.Dispose();
        if (IsPrimary) mutex.ReleaseMutex();
        mutex.Dispose();
    }
}
