using System.Windows;

namespace Pomodoro.Themes;

// Each independently rendered window/control owns styles but follows one palette.
public sealed class ThemeResources : ResourceDictionary
{
    private bool wallpaperAware;
    public bool WallpaperAware
    {
        get => wallpaperAware;
        set { wallpaperAware = value; ThemeManager.Refresh(this); }
    }
    public ThemeResources()
    {
        Source = new Uri("/Pomodoro;component/Themes/Theme.xaml", UriKind.Relative);
        ThemeManager.Register(this);
    }
}

