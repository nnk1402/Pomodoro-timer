using Pomodoro.Shell;
using Pomodoro.Features.Timer;
using Pomodoro.Features.Background;
using Pomodoro.Features.Settings;
using Pomodoro.Services;
using Pomodoro.Themes;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Pomodoro;
using Pomodoro.Features.Timer.Pages;

internal static class Program
{
    private static readonly string Output = System.IO.Path.GetFullPath("artifacts/background-tests");
    private static string Bright => System.IO.Path.Combine(Output, "bright.png");
    private static string Dark => System.IO.Path.Combine(Output, "dark.png");
    private static string Video => System.IO.Path.Combine(Output, "valid.mp4");
    private static BackgroundOptions ImageOptions => new(BackgroundKind.Image, Bright);

    [STAThread]
    private static int Main(string[] args)
    {
        _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        Directory.CreateDirectory(Output);
        MakeImage(Bright, Colors.White);
        MakeImage(Dark, Colors.Black);
        if (args.Contains("--inspect"))
        {
            var main = new MainWindow();
            main.Show();
            main.Wallpaper.Apply(ImageOptions with { SurfaceOpacity = 0.8 });
            main.Closed += (_, _) => Application.Current.Shutdown();
            Application.Current.Run();
            return 0;
        }
        var tests = new (string, Action)[]
        {
            ("Relocated WPF resources and default alarm are packaged", RelocatedResources),
            ("Old settings, background JSON and validation", Persistence),
            ("JPG/PNG cover, no image file lock, repeated changes", Images),
            ("Missing/corrupt image and video safely fall back", InvalidSources),
            ("Deleting, moving or modifying an active image falls back", ChangedSource),
            ("Cancel and window close restore previews", Cancel),
            ("Save commits the preview and reloads correctly", Save),
            ("Failed save leaves saved preferences unchanged", FailedSave),
            ("Theme and background changes preserve timer and alarm", TimerAndTheme),
            ("Actual MP4 playback, loop, pause/restore, reduced motion and file release", VideoLifecycle),
            ("Changing timer modes retains the same background player and position", ModeSwitch),
            ("Light/dark readability renders, including minimum window", RenderReadability),
        };
        int failed = 0;
        foreach (var (name, run) in tests)
        {
            try { run(); Console.WriteLine("PASS: " + name); }
            catch (Exception e) { failed++; Console.WriteLine("FAIL: " + name + "\n" + e.GetBaseException()); }
        }
        Console.WriteLine($"{tests.Length - failed}/{tests.Length} passed");
        Application.Current.Shutdown();
        return failed == 0 ? 0 : 1;
    }

    private static SettingsData Snapshot() => new(AppSetting.workTime, AppSetting.restTime,
        AppSetting.longRestTime, AppSetting.CustomAlarmPath, AppSetting.Theme, AppSetting.Background);

    private static void RelocatedResources()
    {
        foreach (string resource in new[] { "Shell/MainWindow.xaml", "Themes/Theme.xaml",
            "Features/Settings/SettingsWindow.xaml", "Features/Timer/Controls/TimerDisplay.xaml",
            "Features/Timer/Pages/WorkPage.xaml", "Features/Timer/Pages/RestPage.xaml",
            "Features/Timer/Pages/LongRestPage.xaml", "Features/Timer/Pages/TimeUpPage.xaml",
            "Assets/Images/studyTime.jpg" })
        {
            var info = Application.GetResourceStream(new Uri("/Pomodoro;component/" + resource, UriKind.Relative));
            Check(info is not null, "Resource missing: " + resource);
            using var stream = info!.Stream;
            Check(stream.Length > 0, "Empty resource: " + resource);
        }
        Check(File.Exists(Alarm.defaultPath), "Default alarm was not copied to the new Assets/Sounds path");
        Check(new FileInfo(Alarm.defaultPath).Length > 0, "Default alarm is empty");
    }

    private static void Persistence()
    {
        var old = JsonSerializer.Deserialize<SettingsData>("{\"WorkMinutes\":31,\"Theme\":\"Dark\"}")!;
        Check(old.Background is null && old.WorkMinutes == 31 && AppSetting.IsValid(old), "Old settings incompatible");
        foreach (var kind in Enum.GetValues<BackgroundKind>())
        {
            var value = new SettingsData(Background: new(kind, Bright, 0.65, 0.65));
            Check(JsonSerializer.Deserialize<SettingsData>(JsonSerializer.Serialize(value)) == value, "JSON differs");
        }
        Check(!AppSetting.IsValid(new(Background: new(Darkness: double.NaN))), "NaN accepted");
        Check(AppSetting.IsValid(new(Background: new(SurfaceOpacity: 0.2))), "User-configured low opacity rejected");
        Check(!AppSetting.IsValid(new(Background: new(SurfaceOpacity: -0.01))), "Negative opacity accepted");
        Check(!AppSetting.IsValid(new(Background: new(SurfaceOpacity: 0.81))), "Over-limit opacity accepted");
        var original = Snapshot();
        try
        {
            string path = System.IO.Path.Combine(Output, "roundtrip.json");
            Check(AppSetting.Save(original with { Background = ImageOptions }, path, out _), "Save failed");
            var saved = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(path))!;
            AppSetting.Apply(saved);
            Check(AppSetting.Background == ImageOptions, "Reload differs");
            Check(!AppSetting.Save(saved with { Background = new(BackgroundKind.Image, Bright + ".missing") }, path, out _), "Missing file saved");
        }
        finally { AppSetting.Apply(original); }
    }

    private static void Images()
    {
        var main = OpenMain();
        try
        {
            var player = main.Wallpaper.Player;
            foreach (string path in new[] { Bright, Dark, System.IO.Path.GetFullPath("Pomodoro/Assets/Images/studyTime.jpg") })
            {
                main.Wallpaper.Apply(new(BackgroundKind.Image, path));
                Check(main.Wallpaper.Error is null && main.Wallpaper.Current.Path == path, "Valid image rejected");
                using var unlocked = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
            }
            for (int i = 0; i < 25; i++)
            {
                main.Wallpaper.Apply(ImageOptions);
                main.Wallpaper.Apply(new());
            }
            Check(ReferenceEquals(player, main.Wallpaper.Player), "Allocated another player");
            Check(((Image)main.FindName("WallpaperImage")).Source is null, "Default retains image");
            Check(((Image)main.FindName("WallpaperImage")).Stretch == Stretch.UniformToFill, "No aspect preserving crop");
        }
        finally { main.Close(); }
    }

    private static void InvalidSources()
    {
        var main = OpenMain();
        try
        {
            foreach (var option in new[] { new BackgroundOptions(BackgroundKind.Image, Bright + ".missing"),
                new BackgroundOptions(BackgroundKind.Video, Video + ".missing"),
                new BackgroundOptions(BackgroundKind.Image, Corrupt("broken.png")),
                new BackgroundOptions(BackgroundKind.Video, Corrupt("broken.mp4")) })
            {
                main.Wallpaper.Apply(option);
                Wait(() => !main.Wallpaper.IsLoading);
                Check(main.Wallpaper.Current.Kind == BackgroundKind.Default && main.Wallpaper.Error is not null, "No fallback/error");
                Check(((TextBlock)main.FindName("BackgroundNotice")).Visibility == Visibility.Visible, "Missing error UI");
            }
            main.Wallpaper.Apply(new());
            Check(main.Wallpaper.Error is null, "Reset did not clear error");
        }
        finally { main.Close(); }
    }

    private static void Cancel()
    {
        var original = Snapshot();
        var main = OpenMain();
        try
        {
            main.Wallpaper.Apply(ImageOptions);
            var setting = new SettingsWindow(main.Wallpaper) { Owner = main };
            main.Wallpaper.Apply(new(BackgroundKind.Image, Dark, 0.6, 0.65));
            ((RadioButton)setting.FindName("DarkTheme")).IsChecked = true;
            setting.Close();
            Check(main.Wallpaper.Current == ImageOptions && Snapshot() == original, "Close changed original settings");
            setting = new SettingsWindow(main.Wallpaper) { Owner = main };
            setting.Dispatcher.BeginInvoke(new Action(() =>
            {
                Invoke(setting, "setDefaultButton_Click", null, new RoutedEventArgs());
                setting.DialogResult = false;
            }));
            Check(setting.ShowDialog() == false && main.Wallpaper.Current == ImageOptions, "Cancel lost original");
            Check(!main.Wallpaper.IsLoading, "Cancel still opening");
        }
        finally { main.Close(); AppSetting.Apply(original); }
    }

    private static void ChangedSource()
    {
        var main = OpenMain();
        try
        {
            string copy = System.IO.Path.Combine(Output, "changing.png");
            for (int action = 0; action < 3; action++)
            {
                File.Copy(Bright, copy, true);
                main.Wallpaper.Apply(new(BackgroundKind.Image, copy));
                Check(main.Wallpaper.Error is null, "Copy failed to load");
                if (action == 0) File.Delete(copy);
                else if (action == 1) File.Move(copy, copy + ".moved", true);
                else File.WriteAllText(copy, "damaged");
                Wait(() => main.Wallpaper.Current.Kind == BackgroundKind.Default);
                Check(main.Wallpaper.Error is not null, "Source changes have no notification");
            }
        }
        finally { main.Close(); }
    }

    private static void Save()
    {
        var original = Snapshot();
        var main = OpenMain();
        string path = System.IO.Path.Combine(Output, "dialog-save.json");
        try
        {
            var setting = new SettingsWindow(main.Wallpaper, path) { Owner = main };
            setting.Dispatcher.BeginInvoke(new Action(() =>
            {
                main.Wallpaper.Apply(ImageOptions);
                ((Slider)setting.FindName("DarknessSlider")).Value = 45;
                ((RadioButton)setting.FindName("DarkTheme")).IsChecked = true;
                Invoke(setting, "SaveButton_Click", null, new RoutedEventArgs());
            }));
            Check(setting.ShowDialog() == true, "Save did not close successfully");
            Check(main.Wallpaper.Current.Darkness == 0.45 && AppSetting.Background == main.Wallpaper.Current, "Preview not committed");
            var saved = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(path))!;
            Check(saved.Background == main.Wallpaper.Current && saved.Theme == AppTheme.Dark, "Disk differs from preview");
        }
        finally { main.Close(); AppSetting.Apply(original); }
    }

    private static void FailedSave()
    {
        var original = Snapshot();
        var main = OpenMain();
        try
        {
            var setting = new SettingsWindow(main.Wallpaper, Bright + "/settings.json") { Owner = main };
            main.Wallpaper.Apply(ImageOptions);
            Invoke(setting, "SaveButton_Click", null, new RoutedEventArgs());
            Check(Snapshot() == original && setting.IsLoaded == false, "Failed save committed state");
            Check(((TextBlock)setting.FindName("ErrorText")).Visibility == Visibility.Visible, "Save error not shown");
            setting.Close();
            Check(main.Wallpaper.Current.Kind == BackgroundKind.Default, "Failed save/cancel retains preview");
        }
        finally { main.Close(); }
    }

    private static void TimerAndTheme()
    {
        var original = Snapshot();
        var main = OpenMain();
        try
        {
            var page = (TimerPage)((Frame)main.FindName("frMain")).Content;
            page.resume();
            var session = page.Session;
            var remaining = session.Remaining;
            var alarm = Alarm._player;
            string alarmPath = Alarm.path;
            foreach (var theme in new[] { AppTheme.Light, AppTheme.Dark, AppTheme.System })
            {
                ThemeManager.Apply(theme);
                main.Wallpaper.Apply(ImageOptions with { SurfaceOpacity = 0.8 });
                Check(ReferenceEquals(page.Session, session) && page.IsRunning && session.Remaining <= remaining,
                    "Background reset timer");
                var surface = (SolidColorBrush)main.FindResource("SurfaceBrush");
                Check(surface.Opacity == 0.8 && ((Brush)main.FindResource("InkBrush")).Opacity == 1, "Opacity affected text");
                var setting = new SettingsWindow(main.Wallpaper);
                Check(((Brush)setting.FindResource("SurfaceBrush")).Opacity == 1, "Dialog became transparent");
                setting.Close();
            }
            Check(ReferenceEquals(alarm, Alarm._player) && Alarm.path == alarmPath, "Alarm was changed");
            Check(remaining - session.Remaining < TimeSpan.FromSeconds(5), "Unexpected timer jump");
        }
        finally { main.Close(); AppSetting.Apply(original); }
    }

    private static void VideoLifecycle()
    {
        Check(File.Exists(Video), "Provide artifacts/background-tests/valid.mp4 to test the native decoder");
        bool reduced = false;
        var image = new Image { Stretch = Stretch.UniformToFill };
        var shade = new Rectangle { Fill = Brushes.Black };
        var host = new Window { Width = 640, Height = 360, Content = new Grid { Children = { image, shade } } };
        using var background = new BackgroundController(host, image, shade, () => reduced);
        host.Show();
        try
        {
            var player = background.Player;
            background.Apply(new(BackgroundKind.Video, Video));
            Wait(() => !background.IsLoading);
            Check(background.Error is null && background.IsPlaying && player.HasVideo, background.Error ?? "Video not playing");
            Wait(() => player.Position > TimeSpan.FromSeconds(0.4));
            Check(player.IsMuted && player.Volume == 0, "Video not muted");
            host.WindowState = WindowState.Minimized;
            Pump(300);
            var position = player.Position;
            Pump(400);
            Check(!background.IsPlaying && Math.Abs((player.Position - position).TotalSeconds) < 0.15, "Minimized video runs");
            host.WindowState = WindowState.Normal;
            Wait(() => background.IsPlaying && player.Position > position + TimeSpan.FromSeconds(0.2));
            reduced = true;
            background.UpdatePlayback();
            Check(!background.IsPlaying && image.Source is DrawingImage, "Reduced motion not a still frame");
            reduced = false;
            background.UpdatePlayback();
            Check(background.IsPlaying, "Reduced motion restore failed");
            if (player.NaturalDuration.HasTimeSpan)
            {
                Console.WriteLine($"  Native video: {player.NaturalVideoWidth}x{player.NaturalVideoHeight}, {player.NaturalDuration.TimeSpan.TotalSeconds:0.0}s");
                player.Position = player.NaturalDuration.TimeSpan - TimeSpan.FromSeconds(0.3);
                Wait(() => player.Position < TimeSpan.FromSeconds(3), 8000);
                Check(background.IsPlaying, "Loop did not resume");
            }
            for (int i = 0; i < 6; i++)
            {
                background.Apply(ImageOptions);
                AssertUnlocked(Video);
                background.Apply(new(BackgroundKind.Video, Video));
                Wait(() => !background.IsLoading);
                Check(background.Error is null, "Repeated video open failed");
            }
            background.Apply(new());
            AssertUnlocked(Video);
            // Opening requests superseded rapidly must not revive an obsolete source.
            for (int i = 0; i < 10; i++)
            {
                background.Apply(new(BackgroundKind.Video, Video));
                background.Apply(new());
            }
            Pump(500);
            Check(background.Current.Kind == BackgroundKind.Default && image.Source is null, "Stale callback restored video");
            for (int i = 0; i < 6; i++)
            {
                background.Apply(new(BackgroundKind.Video, Corrupt("invalid-race.mp4")));
                background.Apply(new(BackgroundKind.Video, Video));
                Wait(() => !background.IsLoading);
                Check(background.Current.Path == Video && background.Error is null, "Obsolete media callback affected newer video");
            }
            background.Apply(new(BackgroundKind.Video, Video));
            Wait(() => !background.IsLoading);
            background.Dispose();
            AssertUnlocked(Video);
            Check(ReferenceEquals(player, background.Player), "More than one player allocated");
        }
        finally { host.Close(); }
    }

    private static void ModeSwitch()
    {
        var main = OpenMain();
        try
        {
            main.Wallpaper.Apply(new(BackgroundKind.Video, Video));
            Wait(() => !main.Wallpaper.IsLoading);
            Check(main.Wallpaper.Error is null, main.Wallpaper.Error ?? "Video error");
            var player = main.Wallpaper.Player;
            Pump(600);
            var position = player.Position;
            foreach (string method in new[] { "btnRest_Click", "btnLongRest_Click", "btnWork_Click" })
            {
                Invoke(main, method, null, new RoutedEventArgs());
                Pump(50);
                Check(ReferenceEquals(player, main.Wallpaper.Player) && player.Position >= position, "Mode restarted video");
            }
            main.Close();
            AssertUnlocked(Video);
        }
        finally { main.Close(); }
    }

    private static void RenderReadability()
    {
        var main = OpenMain();
        try
        {
            foreach (var theme in new[] { AppTheme.Light, AppTheme.Dark })
            foreach (var path in new[] { Bright, Dark })
            {
                ThemeManager.Apply(theme);
                main.Wallpaper.Apply(new(BackgroundKind.Image, path, 0, 0.8));
                Pump(60);
                var backdrop = (SolidColorBrush)main.FindResource("ContentBackdropBrush");
                var muted = (SolidColorBrush)main.FindResource("MutedBrush");
                var bg = path == Bright ? Colors.White : Colors.Black;
                var composited = Composite(backdrop.Color, bg, backdrop.Opacity);
                Check(Contrast(muted.Color, composited) >= 4.5, $"Muted contrast too low: {Contrast(muted.Color, composited)}");
                Render(main, $"{theme}-{System.IO.Path.GetFileNameWithoutExtension(path)}.png");
            }
            main.Width = 820; main.Height = 700;
            Render(main, "compact-background.png");
            var setting = new SettingsWindow(main.Wallpaper) { Owner = main };
            setting.Show();
            Render(setting, "settings-background.png");
            setting.Close();
        }
        finally { main.Close(); ThemeManager.Apply(AppSetting.Theme); }
    }

    private static MainWindow OpenMain()
    {
        var main = new MainWindow();
        main.Show();
        Pump(50);
        return main;
    }
    private static void AssertUnlocked(string path)
    {
        Wait(() => { try { using var file = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None); return true; } catch (IOException) { return false; } }, 3000);
    }
    private static string Corrupt(string name)
    {
        string path = System.IO.Path.Combine(Output, name);
        File.WriteAllText(path, "not valid media");
        return path;
    }
    private static void Wait(Func<bool> condition, int timeout = 18000)
    {
        var clock = Stopwatch.StartNew();
        while (!condition()) { if (clock.ElapsedMilliseconds > timeout) throw new TimeoutException("Native media condition timed out"); Pump(40); }
    }
    private static void Pump(int ms)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }
    private static void MakeImage(string path, Color color)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(color), null, new Rect(0, 0, 1600, 900));
            // Extreme alternating patches exercise readability across the whole canvas.
            dc.DrawRectangle(new SolidColorBrush(color == Colors.White ? Colors.Black : Colors.White), null, new Rect(300, 0, 400, 900));
        }
        var bitmap = new RenderTargetBitmap(1600, 900, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        WritePng(bitmap, path);
    }
    private static void Render(Window window, string name)
    {
        window.UpdateLayout();
        var root = (FrameworkElement)window.Content;
        int width = (int)(root.ActualWidth + root.Margin.Left + root.Margin.Right);
        int height = (int)(root.ActualHeight + root.Margin.Top + root.Margin.Bottom);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen()) dc.DrawRectangle(window.Background, null, new Rect(0, 0, width, height));
        bitmap.Render(visual); bitmap.Render(root);
        WritePng(bitmap, System.IO.Path.Combine(Output, name));
    }
    private static void WritePng(BitmapSource bitmap, string path)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
    private static Color Composite(Color fg, Color bg, double opacity) => Color.FromRgb(
        (byte)(fg.R * opacity + bg.R * (1 - opacity)), (byte)(fg.G * opacity + bg.G * (1 - opacity)), (byte)(fg.B * opacity + bg.B * (1 - opacity)));
    private static double Contrast(Color a, Color b)
    {
        static double L(Color c)
        {
            static double V(byte v) { double s = v / 255.0; return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
            return V(c.R) * 0.2126 + V(c.G) * 0.7152 + V(c.B) * 0.0722;
        }
        return (Math.Max(L(a), L(b)) + 0.05) / (Math.Min(L(a), L(b)) + 0.05);
    }
    private static void Invoke(object target, string name, params object?[] args) => target.GetType()
        .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(target, args);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
