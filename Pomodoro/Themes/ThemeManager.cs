using System.IO;
using System.Security;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Pomodoro.Themes;

internal static class ThemeManager
{
    private static readonly List<WeakReference<ResourceDictionary>> dictionaries = [];
    public static AppTheme Preference { get; private set; } = AppTheme.System;
    public static bool IsDark { get; private set; } = SystemIsDark();
    private static bool backgroundActive;
    private static double surfaceOpacity = 0.9;

    internal static void SetBackground(bool active, double opacity)
    {
        backgroundActive = active;
        surfaceOpacity = opacity;
        for (int i = dictionaries.Count - 1; i >= 0; i--)
            if (dictionaries[i].TryGetTarget(out var resources)) ApplyPalette(resources);
            else dictionaries.RemoveAt(i);
    }

    internal static void Refresh(ResourceDictionary resources) => ApplyPalette(resources);

    internal static bool ResolveDark(AppTheme preference, bool systemDark) =>
        preference == AppTheme.Dark || (preference == AppTheme.System && systemDark);

    internal static void Register(ResourceDictionary resources)
    {
        dictionaries.Add(new WeakReference<ResourceDictionary>(resources));
        ApplyPalette(resources);
    }

    public static void Apply(AppTheme preference)
    {
        Preference = preference;
        IsDark = ResolveDark(preference, SystemIsDark());
        for (int i = dictionaries.Count - 1; i >= 0; i--)
        {
            if (dictionaries[i].TryGetTarget(out var resources)) ApplyPalette(resources);
            else dictionaries.RemoveAt(i);
        }
        if (Application.Current is { } app)
            foreach (Window window in app.Windows) UpdateTitleBar(window);
    }

    internal static void UpdateTitleBar(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        int enabled = IsDark ? 1 : 0;
        // Older Windows versions may ignore this attribute; the app palette still works.
        DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    private static void ApplyPalette(ResourceDictionary resources)
    {
        var colors = new Dictionary<string, string>
        {
            ["CanvasBrush"] = IsDark ? "#101A1B" : "#F5F7F8",
            ["SurfaceBrush"] = IsDark ? "#182526" : "#FFFFFF",
            ["InkBrush"] = IsDark ? "#E5F0ED" : "#172E30",
            ["MutedBrush"] = backgroundActive ? (IsDark ? "#E0ECE8" : "#243D40") : (IsDark ? "#A6BAB5" : "#596D70"),
            ["AccentBrush"] = IsDark ? "#78D6BC" : "#146B5E",
            ["AccentSoftBrush"] = IsDark ? "#203E36" : "#E7F2EC",
            ["LineBrush"] = IsDark ? "#344846" : "#DCE5E3",
            ["TrackBrush"] = IsDark ? "#263A36" : "#E6EEEB",
            ["HoverBrush"] = IsDark ? "#263735" : "#EDF1F0",
            ["OnAccentBrush"] = IsDark ? "#102B25" : "#FFFFFF",
            ["ErrorBrush"] = IsDark ? "#FFB4A9" : "#AE342A",
            ["ContentBackdropBrush"] = IsDark ? "#101A1B" : "#F5F7F8",
            ["DisabledBrush"] = IsDark ? "#344846" : "#DCE5E3",
            ["DisabledInkBrush"] = IsDark ? "#A6BAB5" : "#596D70",
            ["PrimaryHoverBrush"] = IsDark ? "#98E5CF" : "#10594E",
            ["PrimaryPressedBrush"] = IsDark ? "#60BDA4" : "#0C483F",
        };
        foreach (var (key, value) in colors)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
            if (backgroundActive && resources is ThemeResources { WallpaperAware: true }
                && key is "SurfaceBrush" or "ContentBackdropBrush") brush.Opacity = surfaceOpacity;
            brush.Freeze();
            resources[key] = brush;
        }
        bool protectText = backgroundActive && resources is ThemeResources { WallpaperAware: true }
            && !SystemParameters.HighContrast;
        resources["WallpaperTextShadow"] = protectText ? CreateTextShadow(false) : null!;
        resources["WallpaperTimerShadow"] = protectText ? CreateTextShadow(true) : null!;
    }

    private static DropShadowEffect CreateTextShadow(bool timer)
    {
        // Light ink gets a white halo; light text in dark mode gets a dark shadow.
        // The short radius preserves glyph edges without a wide neon glow.
        var effect = new DropShadowEffect
        {
            Color = IsDark ? Colors.Black : Colors.White,
            BlurRadius = timer ? 6 : 3.5,
            ShadowDepth = IsDark ? (timer ? 1.5 : 1) : 0,
            Direction = 270,
            Opacity = IsDark ? 0.95 : 1,
            RenderingBias = RenderingBias.Performance
        };
        effect.Freeze();
        return effect;
    }

    private static bool SystemIsDark()
    {
        try
        {
            return Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is int value && value == 0;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SecurityException)
        {
            return false;
        }
    }

    public static void StartListening() => SystemEvents.UserPreferenceChanged += SystemPreferenceChanged;
    public static void StopListening() => SystemEvents.UserPreferenceChanged -= SystemPreferenceChanged;

    private static void SystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted) return;
        dispatcher.BeginInvoke(new Action(() =>
        {
            // Refresh contrast/effects as accessibility preferences change, while
            // retaining an explicitly selected light/dark palette.
            Apply(Preference);
        }));
    }
}
