using System;
using System.Windows;
using System.Windows.Media;
using Pomodoro.Features.Todo;
using Pomodoro.Themes;
internal static class Preview
{
    [STAThread] private static void Main()
    {
        var app = new Application();
        ThemeManager.Apply(AppTheme.Dark);
        string path = @"C:\Users\ADMIN\source\repos\Pomodoro\artifacts\todo-tests\native-drag.json";
        var view = new TodoListView(path);
        if (view.Items.Count == 0)
        {
            view.AddTask("Một - học WPF"); view.AddTask("Hai - viết ghi chú"); view.AddTask("Ba - nghỉ mắt");
        }
        var host = new Window { Title = "Kiểm tra Task List · Pomodoro", Content = view, Width = 400, Height = 720,
            Background = new SolidColorBrush(Color.FromRgb(24,37,38)), WindowStartupLocation = WindowStartupLocation.CenterScreen };
        app.Run(host);
    }
}
