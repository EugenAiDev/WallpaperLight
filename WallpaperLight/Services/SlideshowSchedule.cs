using WallpaperLight.Models;

namespace WallpaperLight.Services;

// Uses monotonic time: changing the clock cannot trigger a burst of wallpaper changes.
internal sealed class SlideshowSchedule(TimeProvider? clock = null, Random? random = null)
{
    private readonly TimeProvider clock = clock ?? TimeProvider.System;
    private readonly Random random = random ?? Random.Shared;
    private SlideshowSettings settings = new();
    private long started;
    private TimeSpan? delay;
    private bool suspended;
    private bool resumed;
    private long version;
    public bool AfterResume { get; private set; }
    public TimeSpan? Remaining => delay is { } duration
        ? TimeSpan.FromTicks(Math.Max(0, (duration - this.clock.GetElapsedTime(started)).Ticks)) : null;
    public bool IsDue => !suspended && Remaining == TimeSpan.Zero;
    public long Version => version;
    public bool IsSuspended => suspended;

    public void Configure(SlideshowSettings value)
    {
        if (!Enum.IsDefined(value.Mode) || value.CustomMinutes is < 1 or > 1440)
            throw new ArgumentOutOfRangeException(nameof(value));
        settings = value;
        Schedule(false);
    }

    public void Suspend()
    {
        suspended = true;
        resumed = false;
        delay = null;
        version++;
    }

    public void Resume(bool newCycle = false)
    {
        // Windows can send both automatic and interactive resume notifications.
        if (resumed && !suspended && !newCycle) return;
        suspended = false;
        resumed = true;
        Schedule(true);
    }

    public void Complete(long operationVersion)
    {
        // A resume or settings change during a folder scan owns the newer schedule.
        if (operationVersion == version) Schedule(false);
    }

    private void Schedule(bool afterResume)
    {
        version++;
        AfterResume = afterResume;
        started = this.clock.GetTimestamp();
        if (!settings.Enabled || suspended) { delay = null; return; }
        int minutes = afterResume ? this.random.Next(1, 31) : settings.Mode switch
        {
            IntervalMode.ThirtyMinutes => 30,
            IntervalMode.SixtyMinutes => 60,
            IntervalMode.Custom => settings.CustomMinutes,
            IntervalMode.Random => this.random.Next(1, 61),
            _ => throw new InvalidOperationException()
        };
        delay = TimeSpan.FromMinutes(minutes);
    }
}
