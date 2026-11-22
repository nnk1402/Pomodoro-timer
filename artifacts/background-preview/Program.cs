using System;
using System.Windows;
using Pomodoro;
internal static class Preview
{
    [STAThread] private static void Main()
    {
        var app = new Application();
        var main = new MainWindow();
        main.Loaded += (_, _) => main.Wallpaper.Apply(new BackgroundOptions(BackgroundKind.Image,
            @"C:\Users\ADMIN\source\repos\Pomodoro\artifacts\background-tests\bright.png", 0, 0.8));
        app.Run(main);
    }
}
