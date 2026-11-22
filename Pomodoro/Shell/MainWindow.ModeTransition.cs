using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Pomodoro.Shell;

public partial class MainWindow
{
    private bool modeHighlightPositioned;
    private double modeHighlightTarget = double.NaN;
    private bool listeningForModeMotion;

    private void ConfigureModeTransition()
    {
        Loaded += (_, _) =>
        {
            UpdateModeHighlight(false);
            if (listeningForModeMotion) return;
            listeningForModeMotion = true;
            SystemParameters.StaticPropertyChanged += ModeMotionPreferenceChanged;
        };
        ModeNavigation.SizeChanged += (_, _) => UpdateModeHighlight(false);
        Unloaded += (_, _) =>
        {
            ModeHighlightOffset.BeginAnimation(TranslateTransform.YProperty, null);
            modeHighlightPositioned = false;
            if (!listeningForModeMotion) return;
            SystemParameters.StaticPropertyChanged -= ModeMotionPreferenceChanged;
            listeningForModeMotion = false;
        };
    }

    private void ModeMotionPreferenceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (Dispatcher.HasShutdownStarted) return;
        Dispatcher.BeginInvoke(new Action(() => UpdateModeHighlight(false)));
    }

    private void UpdateModeHighlight(bool animate)
    {
        if (!IsLoaded || ModeNavigation is null || ModeHighlight is null) return;
        var selected = mode switch
        {
            Mode.Rest => RestMode,
            Mode.LongRest => LongRestMode,
            _ => WorkMode
        };
        if (selected.ActualWidth <= 0 || selected.ActualHeight <= 0) return;
        double target = selected.TranslatePoint(new Point(), ModeNavigation).Y;
        ModeHighlight.Width = selected.ActualWidth;
        ModeHighlight.Height = selected.ActualHeight;
        ModeHighlight.Visibility = Visibility.Visible;
        if (animate && modeHighlightPositioned && Math.Abs(modeHighlightTarget - target) < 0.1) return;

        // Continue from the current displayed position when clicking rapidly.
        double from = ModeHighlightOffset.Y;
        ModeHighlightOffset.BeginAnimation(TranslateTransform.YProperty, null);
        ModeHighlightOffset.Y = target;
        bool allowMotion = SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;
        if (animate && modeHighlightPositioned && allowMotion && Math.Abs(from - target) > 0.1)
        {
            ModeHighlightOffset.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(from, target, TimeSpan.FromMilliseconds(200))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.Stop
                });
        }
        modeHighlightTarget = target;
        modeHighlightPositioned = true;
    }
}
