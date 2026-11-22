using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Pomodoro;
using Pomodoro.Controls;
using Pomodoro.Page;

internal static class Program
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--render"))
        {
            RenderPreviews();
            return 0;
        }
        var tests = new (string Name, Action Run)[]
        {
            ("Timers complete at zero, once, and stop after unload", TimersReachZero),
            ("Delayed UI ticks do not slow the countdown", DelayedTicks),
            ("Pause excludes elapsed time and resume is accurate", PauseResume),
            ("All modes use their configured duration", ModeDurations),
            ("Long durations display total minutes without wrapping", LongDurationDisplay),
            ("Completion waits for Settings and happens only once", DeferredCompletion),
            ("Leaving Time up cancels pending navigation", CancelStaleCompletion),
            ("Missing alarm advances without waiting for MediaEnded", MissingAlarm),
            ("Media failure advances only once", FailedAlarm),
            ("Long rest starts after four Work completions", FourWorkSessions),
            ("Default and Cancel leave existing preferences unchanged", CancelSettings),
            ("Invalid settings are rejected without modifying preferences", InvalidSettings),
            ("Preferences persist and failed saves do not change state", PersistSettings),
            ("Reset and switching modes stop the previous countdown", ResetAndSwitch),
            ("Theme changes update existing controls without resetting the timer", LiveThemeSwitch),
            ("Theme preview is reverted when Settings is cancelled", CancelThemePreview),
            ("Theme preferences round-trip and older settings remain compatible", ThemePersistence),
            ("System theme follows the OS while explicit choices override it", SystemThemeResolution),
        };
        int failures = 0;
        foreach (var test in tests)
        {
            try { test.Run(); Console.WriteLine($"PASS: {test.Name}"); }
            catch (Exception error)
            {
                failures++;
                Console.Error.WriteLine($"FAIL: {test.Name}: {error.GetBaseException()}");
            }
        }
        Console.WriteLine($"{tests.Length - failures}/{tests.Length} passed");
        return failures == 0 ? 0 : 1;
    }

    private sealed class ManualClock : TimeProvider
    {
        private long timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;
        public void Advance(TimeSpan amount) => timestamp += amount.Ticks;
    }

    private sealed class ProbePage : TimerPage
    {
        public ProbePage(ManualClock clock, Action complete) =>
            Configure(new TimerDisplay(), () => 1, complete, true, clock);
    }

    private static void TimersReachZero()
    {
        var clock = new ManualClock();
        int transitions = 0;
        var page = new ProbePage(clock, () => transitions++);
        clock.Advance(TimeSpan.FromSeconds(59));
        page.Tick(null, EventArgs.Empty);
        Check(transitions == 0, "Transitioned before zero");
        clock.Advance(TimeSpan.FromSeconds(1));
        page.Tick(null, EventArgs.Empty);
        page.Tick(null, EventArgs.Empty);
        Check(transitions == 1 && !page.IsRunning, "Completion missing or duplicated");
        var unloaded = new ProbePage(clock, () => transitions++);
        unloaded.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        unloaded.resume();
        clock.Advance(TimeSpan.FromMinutes(2));
        unloaded.Tick(null, EventArgs.Empty);
        Check(transitions == 1 && !unloaded.IsRunning, "Unloaded page was restarted");
    }

    private static void DelayedTicks()
    {
        var clock = new ManualClock();
        int transitions = 0;
        var page = new ProbePage(clock, () => transitions++);
        clock.Advance(TimeSpan.FromSeconds(42));
        page.Tick(null, EventArgs.Empty);
        Check(page.Session.Remaining == TimeSpan.FromSeconds(18), "Countdown drifted behind actual elapsed time");
        clock.Advance(TimeSpan.FromSeconds(30));
        page.Tick(null, EventArgs.Empty);
        Check(transitions == 1, "Overdue countdown did not complete");
    }

    private static void PauseResume()
    {
        var clock = new ManualClock();
        var session = new CountdownSession(TimeSpan.FromMinutes(25), clock);
        session.Resume();
        clock.Advance(TimeSpan.FromMinutes(3));
        session.Pause();
        clock.Advance(TimeSpan.FromHours(1));
        Check(session.Remaining == TimeSpan.FromMinutes(22), "Paused time was deducted");
        session.Resume();
        session.Resume();
        clock.Advance(TimeSpan.FromMinutes(2));
        Check(session.Remaining == TimeSpan.FromMinutes(20), "Resume lost remaining time");
    }

    private static void ModeDurations()
    {
        foreach (var (page, minutes) in new (TimerPage, int)[]
        {
            (new workPage(() => { }, false), AppSetting.workTime),
            (new restPage(() => { }, false), AppSetting.restTime),
            (new longRestPage(() => { }, false), AppSetting.longRestTime),
        })
        {
            Check(page.Session.Duration == TimeSpan.FromMinutes(minutes), "Incorrect mode duration");
            Check(!page.IsRunning, "Mode ran before Start");
        }
    }

    private static void LongDurationDisplay()
    {
        var display = new TimerDisplay();
        display.Update(TimeSpan.FromMinutes(125), TimeSpan.FromMinutes(125), false);
        Check(((TextBlock)display.FindName("TimeText")).Text == "125:00", "Minutes wrapped at 60");
    }

    private static void DeferredCompletion()
    {
        int transitions = 0;
        var page = new timeUpPage(() => transitions++);
        page.SuspendTransitions();
        Invoke(page, "_player_MediaEnded", null, EventArgs.Empty);
        Invoke(page, "Button_Click", null, new RoutedEventArgs());
        Check(transitions == 0, "Navigated while Settings was open");
        page.ResumeTransitions();
        page.ResumeTransitions();
        Invoke(page, "_player_MediaEnded", null, EventArgs.Empty);
        Check(transitions == 1, "Completion was lost or duplicated");
    }

    private static void CancelStaleCompletion()
    {
        int transitions = 0;
        var page = new timeUpPage(() => transitions++);
        page.SuspendTransitions();
        Invoke(page, "_player_MediaEnded", null, EventArgs.Empty);
        page.Cancel();
        page.ResumeTransitions();
        Invoke(page, "Button_Click", null, new RoutedEventArgs());
        Check(transitions == 0, "Old page triggered stale navigation");
    }

    private static void MissingAlarm()
    {
        string original = Alarm.path;
        try
        {
            Alarm.path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid()}.mp3");
            int transitions = 0;
            var page = new timeUpPage(() => transitions++);
            page.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Check(transitions == 1, "Missing audio left Time up stuck");
        }
        finally { Alarm.path = original; }
    }

    private static void FailedAlarm()
    {
        int transitions = 0;
        var page = new timeUpPage(() => transitions++);
        Invoke(page, "Player_MediaFailed", null, null);
        Invoke(page, "_player_MediaEnded", null, EventArgs.Empty);
        Check(transitions == 1, "Media failure did not advance exactly once");
    }

    private static void FourWorkSessions()
    {
        var window = new MainWindow();
        var frame = (Frame)Field(window, "frMain");
        for (int session = 1; session <= 4; session++)
        {
            Invoke(window, "goToNextAfterWork");
            PumpDispatcher();
            Check(frame.Content!.GetType() == (session == 4 ? typeof(longRestPage) : typeof(restPage)),
                $"Wrong break after Work session {session}");
        }
        Check((int)Field(window, "_count") == 0, "Cycle counter did not reset");
        Invoke(window, "goToNextAfterWork");
        PumpDispatcher();
        Check(frame.Content is restPage, "Next cycle did not start with a short break");
        window.Close();
    }

    private static void CancelSettings()
    {
        var original = Snapshot();
        try
        {
            AppSetting.Apply(new SettingsData(40, 8, 20));
            var settings = new Setting();
            Check(settings.WorkTimeTextBox.Text == "40", "Current settings were not populated");
            Invoke(settings, "setDefaultButton_Click", null, new RoutedEventArgs());
            Check(AppSetting.workTime == 40 && AppSetting.restTime == 8, "Defaults changed live state before Save");
            settings.Close();
            Check(AppSetting.workTime == 40, "Closing without Save modified preferences");
        }
        finally { AppSetting.Apply(original); }
    }

    private static void InvalidSettings()
    {
        var original = Snapshot();
        Check(!AppSetting.Save(new SettingsData(0, 5, 15), out _), "Zero minutes accepted");
        Check(!AppSetting.Save(new SettingsData(181, 5, 15), out _), "Over-limit minutes accepted");
        Check(Snapshot() == original, "Invalid save changed preferences");
        var settings = new Setting();
        settings.WorkTimeTextBox.Text = "abc";
        Invoke(settings, "SaveButton_Click", null, new RoutedEventArgs());
        Check(((TextBlock)settings.FindName("ErrorText")).Visibility == Visibility.Visible, "Validation feedback missing");
        settings.Close();
    }

    private static void PersistSettings()
    {
        var original = Snapshot();
        string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Pomodoro-tests-" + Guid.NewGuid());
        string destination = System.IO.Path.Combine(folder, "settings.json");
        try
        {
            Check(AppSetting.Save(new SettingsData(35, 7, 18), destination, out _), "Save failed");
            var saved = System.Text.Json.JsonSerializer.Deserialize<SettingsData>(System.IO.File.ReadAllText(destination));
            Check(saved == new SettingsData(35, 7, 18), "Saved values differ");
            Check(!AppSetting.Save(new SettingsData(50, 8, 20), System.IO.Path.Combine(destination, "invalid.json"), out _),
                "Save to invalid location succeeded");
            Check(AppSetting.workTime == 35, "Failed save modified live settings");
        }
        finally
        {
            AppSetting.Apply(original);
            if (System.IO.File.Exists(destination)) System.IO.File.Delete(destination);
            if (System.IO.Directory.Exists(folder)) System.IO.Directory.Delete(folder);
        }
    }

    private static void ResetAndSwitch()
    {
        var window = new MainWindow();
        var frame = (Frame)Field(window, "frMain");
        PumpDispatcher();
        var oldPage = (TimerPage)frame.Content;
        Invoke(window, "btnTimeControl_Click", null, new RoutedEventArgs());
        Check(oldPage.IsRunning, "Start did not run");
        Invoke(window, "Reset_Click", null, new RoutedEventArgs());
        PumpDispatcher();
        Check(!oldPage.IsRunning && !((TimerPage)frame.Content).IsRunning, "Reset did not stop old timer and prepare a paused one");
        Invoke(window, "btnRest_Click", null, new RoutedEventArgs());
        PumpDispatcher();
        Check(frame.Content is restPage && !((TimerPage)frame.Content).IsRunning, "Mode selection did not prepare the selected timer");
        window.Close();
    }

    private static SettingsData Snapshot() => new(AppSetting.workTime, AppSetting.restTime, AppSetting.longRestTime, AppSetting.CustomAlarmPath, AppSetting.Theme);

    private static string ColorOf(object brush) => ((System.Windows.Media.SolidColorBrush)brush).Color.ToString();

    private static void LiveThemeSwitch()
    {
        var original = Snapshot();
        var window = new MainWindow();
        PumpDispatcher();
        var page = (TimerPage)((Frame)Field(window, "frMain")).Content;
        page.resume();
        var display = (TimerDisplay)page.FindName("Display");
        try
        {
            ThemeManager.Apply(AppTheme.Dark);
            PumpDispatcher();
            Check(ColorOf(window.Background) == "#FF101A1B", "Window did not update to dark");
            Check(ColorOf(((TextBlock)display.FindName("TimeText")).Foreground) == "#FFE5F0ED", "Existing timer text did not update");
            Check(page.IsRunning && page.Session.Duration == TimeSpan.FromMinutes(AppSetting.workTime), "Theme switch reset the timer");
            ThemeManager.Apply(AppTheme.Light);
            PumpDispatcher();
            Check(ColorOf(window.Background) == "#FFF5F7F8", "Window did not return to light");
            Check(ColorOf(((TextBlock)display.FindName("TimeText")).Foreground) == "#FF172E30", "Timer text did not return to light");
            Check(page.IsRunning, "Switching back stopped the timer");
        }
        finally { window.Close(); AppSetting.Apply(original); }
    }

    private static void CancelThemePreview()
    {
        var original = Snapshot();
        try
        {
            AppSetting.Apply(original with { Theme = AppTheme.Light });
            var settings = new Setting();
            ((RadioButton)settings.FindName("DarkTheme")).IsChecked = true;
            Check(ThemeManager.IsDark && AppSetting.Theme == AppTheme.Light, "Preview changed saved preference or did not apply");
            settings.Close();
            Check(!ThemeManager.IsDark && AppSetting.Theme == AppTheme.Light, "Cancel did not restore light");
        }
        finally { AppSetting.Apply(original); }
    }

    private static void ThemePersistence()
    {
        var old = System.Text.Json.JsonSerializer.Deserialize<SettingsData>("{\"WorkMinutes\":25,\"RestMinutes\":5,\"LongRestMinutes\":15}");
        Check(old?.Theme == AppTheme.System && AppSetting.IsValid(old), "Older settings no longer load");
        foreach (var theme in Enum.GetValues<AppTheme>())
        {
            var value = new SettingsData(Theme: theme);
            var json = System.Text.Json.JsonSerializer.Serialize(value);
            Check(System.Text.Json.JsonSerializer.Deserialize<SettingsData>(json) == value, "Theme did not round-trip");
        }
        Check(!AppSetting.IsValid(new SettingsData(Theme: (AppTheme)99)), "Unknown theme accepted");
    }

    private static void SystemThemeResolution()
    {
        Check(ThemeManager.ResolveDark(AppTheme.System, true), "System dark ignored");
        Check(!ThemeManager.ResolveDark(AppTheme.System, false), "System light ignored");
        Check(ThemeManager.ResolveDark(AppTheme.Dark, false), "Explicit dark ignored");
        Check(!ThemeManager.ResolveDark(AppTheme.Light, true), "Explicit light ignored");
    }

    private static void RenderPreviews()
    {
        string output = System.IO.Path.GetFullPath("docs/screenshots");
        System.IO.Directory.CreateDirectory(output);
        ThemeManager.Apply(AppTheme.Light);
        var main = new MainWindow();
        PumpDispatcher();
        Render(main, 1104, 762, System.IO.Path.Combine(output, "main.png"));
        Render(main, 804, 662, System.IO.Path.Combine(output, "compact.png"));
        var settings = new Setting();
        ((RadioButton)settings.FindName("LightTheme")).IsChecked = true;
        Render(settings, 564, 722, System.IO.Path.Combine(output, "settings.png"));
        Render(settings, 484, 442, System.IO.Path.Combine(output, "settings-compact.png"));
        ((RadioButton)settings.FindName("DarkTheme")).IsChecked = true;
        PumpDispatcher();
        Render(main, 1104, 762, System.IO.Path.Combine(output, "main-dark.png"));
        Render(main, 804, 662, System.IO.Path.Combine(output, "compact-dark.png"));
        Render(settings, 564, 722, System.IO.Path.Combine(output, "settings-dark.png"));
        main.Close();
        settings.Close();
        Console.WriteLine($"Rendered previews to {output}");
    }

    private static void Render(Window window, int width, int height, string destination)
    {
        var root = (FrameworkElement)window.Content;
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
        var visual = new System.Windows.Media.DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle((System.Windows.Media.Brush)window.FindResource("CanvasBrush"), null, new Rect(0, 0, width, height));
        }
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Render(root);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = System.IO.File.Create(destination);
        encoder.Save(stream);
    }

    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static object Field(object instance, string name) =>
        instance.GetType().GetField(name, PrivateInstance)!.GetValue(instance)!;

    private static void Invoke(object instance, string name, params object?[] args) =>
        instance.GetType().GetMethod(name, PrivateInstance)!.Invoke(instance, args);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
