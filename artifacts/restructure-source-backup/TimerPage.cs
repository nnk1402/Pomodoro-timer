using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Pomodoro.Controls;
using Pomodoro.Interface;

namespace Pomodoro;

public class TimerPage : System.Windows.Controls.Page, ITimerControl
{
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private CountdownSession? session;
    private TimerDisplay? display;
    private Func<int>? getMinutes;
    private Action? onComplete;
    private bool unloaded;
    private bool completed;
    private bool hasStarted;
    public bool IsRunning => session?.IsRunning == true;
    internal CountdownSession Session => session!;
    internal void SetNextSession(string text) => display?.SetNextSession(text);

    public TimerPage()
    {
        timer.Tick += Tick;
        Unloaded += OnUnloaded;
    }

    protected void Configure(TimerDisplay display, Func<int> minutes, Action onComplete, bool start, TimeProvider? clock = null)
    {
        this.display = display;
        getMinutes = minutes;
        this.onComplete = onComplete;
        session = new CountdownSession(TimeSpan.FromMinutes(minutes()), clock);
        if (start) resume();
        else Refresh();
    }

    public void pause()
    {
        timer.Stop();
        session?.Pause();
        Refresh();
    }

    public void resume()
    {
        if (unloaded || completed) return;
        hasStarted = true;
        session?.Resume();
        timer.Start();
        Refresh();
    }

    public void update()
    {
        if (unloaded) return;
        session?.Reset(TimeSpan.FromMinutes(getMinutes!()));
        completed = false;
        Refresh();
    }

    internal void Tick(object? sender, EventArgs e)
    {
        if (unloaded || completed || !IsRunning) return;
        Refresh();
        if (session!.Remaining > TimeSpan.Zero) return;
        completed = true;
        pause();
        onComplete?.Invoke();
    }

    private void Refresh()
    {
        if (session is not null) display?.Update(session.Remaining, session.Duration, session.IsRunning, !hasStarted);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        unloaded = true;
        pause();
        timer.Tick -= Tick;
    }
}
