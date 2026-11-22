using System.Windows;
using System.Windows.Input;

namespace Pomodoro.Themes;

/// <summary>Distinguishes in-app Tab navigation from keyboard window activation.</summary>
public static class KeyboardFocusFeedback
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(KeyboardFocusFeedback), new PropertyMetadata(false, EnabledChanged));

    public static readonly DependencyProperty IsKeyboardNavigationProperty = DependencyProperty.RegisterAttached(
        "IsKeyboardNavigation", typeof(bool), typeof(KeyboardFocusFeedback), new PropertyMetadata(false));

    public static bool GetEnabled(DependencyObject target) => (bool)target.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject target, bool value) => target.SetValue(EnabledProperty, value);
    public static bool GetIsKeyboardNavigation(DependencyObject target) => (bool)target.GetValue(IsKeyboardNavigationProperty);
    public static void SetIsKeyboardNavigation(DependencyObject target, bool value) => target.SetValue(IsKeyboardNavigationProperty, value);

    private static void EnabledChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not Window window) return;
        if ((bool)e.NewValue)
        {
            window.PreviewKeyDown += KeyDown;
            window.PreviewMouseDown += MouseDown;
            window.PreviewTouchDown += TouchDown;
            window.Activated += Reset;
            window.Deactivated += Reset;
        }
        else
        {
            window.PreviewKeyDown -= KeyDown;
            window.PreviewMouseDown -= MouseDown;
            window.PreviewTouchDown -= TouchDown;
            window.Activated -= Reset;
            window.Deactivated -= Reset;
        }
        SetIsKeyboardNavigation(window, false);
    }

    private static void KeyDown(object sender, KeyEventArgs e)
    {
        var window = (Window)sender;
        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0 || e.Key == Key.System)
            SetIsKeyboardNavigation(window, false);
        else if (e.Key == Key.Tab && (Keyboard.Modifiers & ~ModifierKeys.Shift) == ModifierKeys.None)
            SetIsKeyboardNavigation(window, true);
    }

    private static void MouseDown(object sender, MouseButtonEventArgs e) => SetIsKeyboardNavigation((Window)sender, false);
    private static void TouchDown(object? sender, TouchEventArgs e)
    {
        if (sender is Window window) SetIsKeyboardNavigation(window, false);
    }
    private static void Reset(object? sender, EventArgs e)
    {
        if (sender is Window window) SetIsKeyboardNavigation(window, false);
    }
}
