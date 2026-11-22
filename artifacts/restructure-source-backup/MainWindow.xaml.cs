using System.Windows;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Pomodoro.Interface;
using Pomodoro.Page;

namespace Pomodoro;

public partial class MainWindow : Window
{
    private const int WorkSessionsBeforeLongRest = 4;
    private int _count;
    private enum Mode { Work, Rest, LongRest }
    private Mode mode = Mode.Work;
    private bool hasStarted;
    internal BackgroundController Wallpaper { get; }

    public MainWindow()
    {
        InitializeComponent();
        Wallpaper = new BackgroundController(this, WallpaperImage, WallpaperShade);
        Wallpaper.Changed += () =>
        {
            BackgroundNotice.Text = Wallpaper.Error ?? "";
            BackgroundNotice.Visibility = Wallpaper.Error is null ? Visibility.Collapsed : Visibility.Visible;
        };
        Loaded += (_, _) => Wallpaper.Apply(AppSetting.Background);
        SourceInitialized += (_, _) => ThemeManager.UpdateTitleBar(this);
        SelectMode(Mode.Work, false);
        Closed += (_, _) =>
        {
            (frMain.Content as ITimerControl)?.pause();
            (frMain.Content as timeUpPage)?.Cancel();
            Alarm._player.Stop();
            Wallpaper.Dispose();
        };
    }

    private void BackgroundLayout_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (CycleBackdrop is null || CycleBlurImage is null || WallpaperImage is null) return;
        // Sample the exact wallpaper region behind the panel, including after resizing.
        var origin = WallpaperImage.TranslatePoint(new Point(0, 0), CycleBackdrop);
        System.Windows.Controls.Canvas.SetLeft(CycleBlurImage, origin.X);
        System.Windows.Controls.Canvas.SetTop(CycleBlurImage, origin.Y);
        CycleBackdrop.Clip = new RectangleGeometry(new Rect(0, 0,
            CycleBackdrop.ActualWidth, CycleBackdrop.ActualHeight), 12, 12);
    }

    private void SelectMode(Mode selected, bool start)
    {
        // Cancel old callbacks before Frame schedules navigation.
        (frMain.Content as ITimerControl)?.pause();
        (frMain.Content as timeUpPage)?.Cancel();
        mode = selected;
        hasStarted = start;
        WorkMode.IsChecked = mode == Mode.Work;
        RestMode.IsChecked = mode == Mode.Rest;
        LongRestMode.IsChecked = mode == Mode.LongRest;
        frMain.Content = mode switch
        {
            Mode.Work => new workPage(() => timeUp(goToNextAfterWork), start),
            Mode.Rest => new restPage(() => timeUp(goToNextAfterRest), start),
            _ => new longRestPage(() => timeUp(goToNextAfterRest), start)
        };
        UpdateChrome(start);
    }

    private void btnWork_Click(object sender, RoutedEventArgs e) => SelectMode(Mode.Work, false);
    private void btnRest_Click(object sender, RoutedEventArgs e) => SelectMode(Mode.Rest, false);
    private void btnLongRest_Click(object sender, RoutedEventArgs e) => SelectMode(Mode.LongRest, false);

    private void goToNextAfterWork()
    {
        _count++;
        if (_count < WorkSessionsBeforeLongRest) SelectMode(Mode.Rest, true);
        else
        {
            _count = 0;
            SelectMode(Mode.LongRest, true);
        }
    }

    private void goToNextAfterRest() => SelectMode(Mode.Work, true);

    private void timeUp(Action next)
    {
        // Notify without changing the user's window size or stealing topmost state.
        StatusText.Text = "Hoàn thành";
        frMain.Content = new timeUpPage(next);
    }

    private void UpdateChrome(bool running)
    {
        StatusText.Text = running ? "Đang tập trung" : hasStarted ? "Đã tạm dừng" : "Sẵn sàng";
        if (running && mode != Mode.Work) StatusText.Text = "Đang nghỉ ngơi";
        string nextSession = mode == Mode.Work
            ? _count + 1 >= WorkSessionsBeforeLongRest
                ? $"Tiếp theo: Nghỉ dài · {AppSetting.longRestTime} phút"
                : $"Tiếp theo: Nghỉ ngắn · {AppSetting.restTime} phút"
            : $"Tiếp theo: Tập trung · {AppSetting.workTime} phút";
        (frMain.Content as TimerPage)?.SetNextSession(nextSession);
        CycleText.Text = $"{_count} / {WorkSessionsBeforeLongRest} phiên hoàn thành";
        var dots = new[] { Dot1, Dot2, Dot3, Dot4 };
        for (int i = 0; i < dots.Length; i++)
            dots[i].SetResourceReference(System.Windows.Shapes.Shape.FillProperty, i < _count ? "AccentBrush" : "LineBrush");
    }

    private void btnTimeControl_Click(object sender, RoutedEventArgs e)
    {
        if (frMain.Content is not ITimerControl current) return;
        if (current.IsRunning) current.pause();
        else { hasStarted = true; current.resume(); }
        UpdateChrome(current.IsRunning);
    }

    private void Reset_Click(object sender, RoutedEventArgs e) => SelectMode(mode, false);

    private int CurrentMinutes => mode switch
    {
        Mode.Work => AppSetting.workTime,
        Mode.Rest => AppSetting.restTime,
        _ => AppSetting.longRestTime
    };

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        var current = frMain.Content as ITimerControl;
        var completion = frMain.Content as timeUpPage;
        bool wasRunning = current?.IsRunning == true;
        int oldMinutes = CurrentMinutes;
        current?.pause();
        completion?.SuspendTransitions();
        try
        {
            var settings = new Setting(Wallpaper) { Owner = this };
            if (settings.ShowDialog() == true && oldMinutes != CurrentMinutes)
                current?.update();
        }
        finally
        {
            if (wasRunning) current?.resume();
            if (current is not null) UpdateChrome(current.IsRunning);
            completion?.ResumeTransitions();
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.OemComma)
        {
            e.Handled = true;
            Button_Click(this, new RoutedEventArgs());
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.R && frMain.Content is ITimerControl)
        {
            e.Handled = true;
            Reset_Click(this, new RoutedEventArgs());
        }
        else if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.None
            && Keyboard.FocusedElement is not ButtonBase && frMain.Content is ITimerControl)
        {
            e.Handled = true;
            btnTimeControl_Click(this, new RoutedEventArgs());
        }
    }
}
