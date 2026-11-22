namespace Pomodoro.Features.Timer;

// Time is calculated from a monotonic clock, rather than counting UI timer events.
internal sealed class CountdownSession
{
    private readonly TimeProvider clock;
    private long startedAt;
    private TimeSpan remaining;
    public TimeSpan Duration { get; private set; }
    public bool IsRunning { get; private set; }
    public TimeSpan Remaining => IsRunning
        ? MaxZero(remaining - clock.GetElapsedTime(startedAt))
        : remaining;

    public CountdownSession(TimeSpan duration, TimeProvider? clock = null)
    {
        this.clock = clock ?? TimeProvider.System;
        Reset(duration);
    }

    public void Reset(TimeSpan duration)
    {
        Duration = MaxZero(duration);
        remaining = Duration;
        startedAt = clock.GetTimestamp();
    }

    public void Resume()
    {
        if (IsRunning || remaining <= TimeSpan.Zero) return;
        startedAt = clock.GetTimestamp();
        IsRunning = true;
    }

    public void Pause()
    {
        if (!IsRunning) return;
        remaining = Remaining;
        IsRunning = false;
    }

    private static TimeSpan MaxZero(TimeSpan value) => value < TimeSpan.Zero ? TimeSpan.Zero : value;
}
