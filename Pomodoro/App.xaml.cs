using Pomodoro.Features.Settings;
using Pomodoro.Themes;
using System.Windows;

namespace Pomodoro;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppSetting.Load();
        ThemeManager.Apply(AppSetting.Theme);
        ThemeManager.StartListening();
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ThemeManager.StopListening();
        base.OnExit(e);
    }
}
