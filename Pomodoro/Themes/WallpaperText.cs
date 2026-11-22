using System.Windows;

namespace Pomodoro.Themes;

// Solid button/selection surfaces already provide contrast. Their descendants
// inherit this opt-out so a text halo does not wash out control labels.
public static class WallpaperText
{
    public static readonly DependencyProperty SuppressShadowProperty = DependencyProperty.RegisterAttached(
        "SuppressShadow", typeof(bool), typeof(WallpaperText),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    public static bool GetSuppressShadow(DependencyObject element) => (bool)element.GetValue(SuppressShadowProperty);
    public static void SetSuppressShadow(DependencyObject element, bool value) => element.SetValue(SuppressShadowProperty, value);
}
