using Pomodoro.Features.Background;
using Pomodoro.Features.Settings;
using Pomodoro.Themes;
using Pomodoro.Services;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Pomodoro.Features.Timer;
using Pomodoro.Features.Timer.Pages;

namespace Pomodoro.Shell;

public partial class MainWindow : Window
{
    private const int WorkSessionsBeforeLongRest = 4;
    private int _count;
    private enum Mode { Work, Rest, LongRest }
    private Mode mode = Mode.Work;
    private bool hasStarted;
    private bool isTodoOpen = true;
    internal BackgroundController Wallpaper { get; }

    public MainWindow()
    {
        InitializeComponent();
        ConfigureModeTransition();
        ConfigureFullScreen();
        Wallpaper = new BackgroundController(this, WallpaperImage, WallpaperShade);
        Wallpaper.Changed += () =>
        {
            BackgroundNotice.Text = Wallpaper.Error ?? "";
            BackgroundNotice.Visibility = Wallpaper.Error is null ? Visibility.Collapsed : Visibility.Visible;
        };
        Loaded += (_, _) =>
        {
            isTodoOpen = ActualWidth >= 1100;
            UpdateTodoLayout();
            Wallpaper.Apply(AppSetting.Background);
        };
        SourceInitialized += (_, _) => ThemeManager.UpdateTitleBar(this);
        SelectMode(Mode.Work, false);
        Closed += (_, _) =>
        {
            (frMain.Content as ITimerControl)?.pause();
            (frMain.Content as TimeUpPage)?.Cancel();
            Alarm._player.Stop();
            TodoList.Flush();
            Wallpaper.Dispose();
        };
    }

    private void BackgroundLayout_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateTodoLayout();
        if (CycleBackdrop is null) return;
        CycleBackdrop.Clip = new RectangleGeometry(new Rect(0, 0,
            CycleBackdrop.ActualWidth, CycleBackdrop.ActualHeight), 12, 12);
        if (TodoBackdrop is null) return;
        double width = TodoBackdrop.ActualWidth, height = TodoBackdrop.ActualHeight;
        double radius = Math.Min(16, Math.Min(width, height) / 2);
        // Round just the left corners; the right edge remains flush with the window.
        var clip = new StreamGeometry();
        using (var context = clip.Open())
        {
            context.BeginFigure(new Point(radius, 0), true, true);
            context.LineTo(new Point(width, 0), true, false);
            context.LineTo(new Point(width, height), true, false);
            context.LineTo(new Point(radius, height), true, false);
            context.ArcTo(new Point(0, height - radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
            context.LineTo(new Point(0, radius), true, false);
            context.ArcTo(new Point(radius, 0), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
        }
        clip.Freeze();
        TodoBackdrop.Clip = clip;
    }

    private void UpdateTodoLayout()
    {
        if (TodoBackdrop is null) return;
        TodoBackdrop.Visibility = isTodoOpen ? Visibility.Visible : Visibility.Collapsed;
        // Keep more tasks visible while leaving room above the bottom status panel.
        TodoBackdrop.Height = Math.Min(540, Math.Max(0, ActualHeight - 240));
    }

    private void TodoToggle_Click(object sender, RoutedEventArgs e)
    {
        isTodoOpen = !isTodoOpen;
        UpdateTodoLayout();
    }

    private void TodoClose_Requested(object? sender, EventArgs e)
    {
        isTodoOpen = false;
        UpdateTodoLayout();
        TodoToggleButton.Focus();
    }

    private void SelectMode(Mode selected, bool start)
    {
        // Cancel old callbacks before Frame schedules navigation.
        (frMain.Content as ITimerControl)?.pause();
        (frMain.Content as TimeUpPage)?.Cancel();
        mode = selected;
        hasStarted = start;
        WorkMode.IsChecked = mode == Mode.Work;
        RestMode.IsChecked = mode == Mode.Rest;
        LongRestMode.IsChecked = mode == Mode.LongRest;
        UpdateModeHighlight(true);
        frMain.Content = mode switch
        {
            Mode.Work => new WorkPage(() => timeUp(goToNextAfterWork), start),
            Mode.Rest => new RestPage(() => timeUp(goToNextAfterRest), start),
            _ => new LongRestPage(() => timeUp(goToNextAfterRest), start)
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
        frMain.Content = new TimeUpPage(next);
    }

    private void UpdateChrome(bool running)
    {
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
        var completion = frMain.Content as TimeUpPage;
        bool wasRunning = current?.IsRunning == true;
        int oldMinutes = CurrentMinutes;
        current?.pause();
        completion?.SuspendTransitions();
        try
        {
            var settings = new SettingsWindow(Wallpaper) { Owner = this };
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
        Key pressed = e.Key == Key.System ? e.SystemKey : e.Key;
        bool fullScreenShortcut = (pressed == Key.F11 && Keyboard.Modifiers == ModifierKeys.None)
            || (pressed == Key.Enter && Keyboard.Modifiers == ModifierKeys.Alt);
        if (fullScreenShortcut)
        {
            e.Handled = true;
            if (!e.IsRepeat) ToggleFullScreen();
            return;
        }
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.OemComma)
        {
            e.Handled = true;
            Button_Click(this, new RoutedEventArgs());
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.R && frMain.Content is ITimerControl
            && !TodoList.IsKeyboardFocusWithin && !IsEditingText(e))
        {
            e.Handled = true;
            Reset_Click(this, new RoutedEventArgs());
        }
        else if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.None
            && Keyboard.FocusedElement is not ButtonBase && Keyboard.FocusedElement is not TextBoxBase
            && !TodoList.IsKeyboardFocusWithin && !IsEditingText(e) && frMain.Content is ITimerControl)
        {
            e.Handled = true;
            btnTimeControl_Click(this, new RoutedEventArgs());
        }
    }

    private static bool IsEditingText(KeyEventArgs e)
    {
        // Use the routed event source as well as keyboard focus: focus can move
        // while an input event is travelling through the window.
        for (DependencyObject? source = e.OriginalSource as DependencyObject; source is not null;)
        {
            if (source is TextBoxBase or System.Windows.Controls.PasswordBox
                or System.Windows.Controls.ComboBox or Pomodoro.Features.Todo.TodoListView) return true;
            source = source is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        }
        return false;
    }
}
