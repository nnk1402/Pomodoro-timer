using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Pomodoro.Features.Background;
using Pomodoro.Features.Todo;
using Pomodoro.Features.Timer;
using Pomodoro.Shell;
using Pomodoro.Themes;

internal static class Program
{
    private static readonly string Output = Path.GetFullPath("artifacts/todo-tests");
    [STAThread]
    private static int Main()
    {
        _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        Directory.CreateDirectory(Output);
        var tests = new (string Name, Action Test)[]
        {
            ("Add, complete, delete and details persist", Persistence),
            ("Manual order survives reload and completed tasks retain position", Ordering),
            ("Missing, damaged and unwritable stores fail safely", StorageErrors),
            ("Enter adds, blank input is rejected, Space leaves timer unchanged", KeyboardInput),
            ("List scrolls with invisible scrollbar", Scrolling),
            ("Compact floating panel, rounded clipping, shared background and themes", LayoutAndRender),
            ("Task creation, drag preview, commit/cancel and reduced motion", MotionAndDrag)
        };
        int failed = 0;
        foreach (var test in tests)
        {
            try { test.Test(); Console.WriteLine("PASS: " + test.Name); }
            catch (Exception e) { failed++; Console.WriteLine("FAIL: " + test.Name + "\n" + e.GetBaseException()); }
        }
        Application.Current.Shutdown();
        Console.WriteLine($"{tests.Length - failed}/{tests.Length} passed");
        return failed == 0 ? 0 : 1;
    }

    private static string FreshPath() => Path.Combine(Output, Guid.NewGuid() + ".json");
    private static void Persistence()
    {
        string path = FreshPath();
        var view = new TodoListView(path);
        Check(!view.AddTask("   ") && view.Items.Count == 0, "Blank task accepted");
        view.AddTask("  Học WPF  "); view.AddTask("Nghỉ mắt");
        view.Items[0].Details = "Đọc tài liệu\n- Binding\n- Resource và tiếng Việt ✓";
        view.Items[0].IsCompleted = true;
        view.Flush();
        var restored = new TodoListView(path);
        Check(restored.Items.Count == 2 && restored.Items[0].Title == "Học WPF", "Task text lost");
        Check(restored.Items[0].IsCompleted && restored.Items[0].Details.Contains("\n- Binding"), "Completion/details lost");
        restored.DeleteTask(restored.Items[1]);
        Check(new TodoListView(path).Items.Count == 1, "Delete not persisted");
        // Older task files did not contain a Details property.
        File.WriteAllText(path, "[{\"Title\":\"Cũ\",\"IsCompleted\":true}]");
        Check(new TodoListView(path).Items[0].Details == "", "Old tasks incompatible");
    }

    private static void Ordering()
    {
        string path = FreshPath();
        var view = new TodoListView(path);
        foreach (string task in new[] { "Một", "Hai", "Ba", "Bốn" }) view.AddTask(task);
        view.Items[1].IsCompleted = true;
        var last = view.Items[3];
        last.Details = "Chi tiết đi theo task";
        view.MoveTask(last, 0);
        view.MoveTask(view.Items[1], 3);
        var restored = new TodoListView(path);
        Check(string.Join(",", restored.Items.Select(item => item.Title)) == "Bốn,Hai,Ba,Một", "Wrong reorder");
        Check(restored.Items[0].Details == last.Details && restored.Items[1].IsCompleted, "Reorder lost task data");
        view.MoveTask(last, 99);
        Check(view.Items[^1] == last, "Move to bottom failed");
        // Exercise the same before/after insertion path used by native row Drop.
        view.DropTask(view.Items[^1], view.Items[0], false);
        Check(view.Items[0] == last, "Drop before first task failed");
        view.DropTask(last, view.Items[^1], true);
        Check(view.Items[^1] == last, "Drop after last task failed");
        view.DropTask(last, last, true);
        Check(view.Items[^1] == last, "Dropping on itself changed order");
        Check(new TodoListView(path).Items[^1].Title == last.Title, "Drop order not saved");
    }

    private static void StorageErrors()
    {
        string path = FreshPath();
        Check(TodoStore.Load(path, out string error).Count == 0 && error == "", "Missing file failed");
        File.WriteAllText(path, "not JSON");
        Check(TodoStore.Load(path, out error).Count == 0 && error.Length > 0, "Corruption not reported");
        var view = new TodoListView(path + "/invalid.json");
        view.AddTask("Giữ trong bộ nhớ");
        Check(view.Items.Count == 1 && ((TextBlock)view.FindName("SaveError")).Visibility == Visibility.Visible,
            "Save failure lost data or lacks feedback");
    }

    private static void KeyboardInput()
    {
        var main = new MainWindow { WindowState = WindowState.Normal, Width = 1360 };
        main.Show(); Pump();
        try
        {
            // Use isolated task storage while exercising the real window's key routing.
            var view = new TodoListView(FreshPath());
            var original = (TodoListView)main.FindName("TodoList");
            ((Border)original.Parent).Child = view;
            var input = (TextBox)view.FindName("DraftText");
            var page = (TimerPage)((Frame)main.FindName("frMain")).Content;
            var session = page.Session;
            input.Focus(); Keyboard.Focus(input); Pump();
            // MainWindow guards text inputs, even if the TodoList instance is replaced.
            input.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(input), 0, Key.Space)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Check(ReferenceEquals(session, page.Session) && !page.IsRunning, "Space in task editor started countdown");
            input.Text = "Viết ghi chú";
            input.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(input), 0, Key.Enter)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Check(view.Items.Count == 1 && input.Text == "", "Enter did not add and clear");
        }
        finally { main.Close(); }
    }

    private static void Scrolling()
    {
        var view = new TodoListView(FreshPath());
        for (int i = 1; i <= 25; i++) view.AddTask("Công việc " + i);
        var host = new Window { Content = view, Width = 320, Height = 600 };
        host.Show(); Pump();
        try
        {
            var scroll = (ScrollViewer)view.FindName("TaskScroll");
            Check(scroll.VerticalScrollBarVisibility == ScrollBarVisibility.Hidden && scroll.ScrollableHeight > 0,
                "Scrollbar visible or list cannot scroll");
            scroll.ScrollToBottom(); Pump();
            Check(scroll.VerticalOffset > 0, "Hidden scrollbar disabled scrolling");
        }
        finally { host.Close(); }
    }

    private static void LayoutAndRender()
    {
        var main = new MainWindow { WindowState = WindowState.Normal, Width = 1440, Height = 900 };
        main.Show(); Pump();
        var panel = (Grid)main.FindName("TodoBackdrop");
        var view = new TodoListView(FreshPath());
        view.AddTask("Hoàn thiện giao diện Pomodoro");
        view.AddTask("Đọc tài liệu WPF và ghi lại ý chính");
        view.AddTask("Nghỉ mắt và uống nước");
        view.Items[0].IsCompleted = true;
        view.Items[1].Details = "Tìm hiểu Binding, ResourceDictionary và cách tổ chức component.\nGhi chú các ví dụ hữu ích.";
        view.Flush();
        var original = (TodoListView)main.FindName("TodoList");
        ((Border)original.Parent).Child = view;
        try
        {
            Check(panel.ActualHeight <= 600 && panel.ActualWidth == 360 && panel.VerticalAlignment == VerticalAlignment.Top,
                "Panel is not compact");
            double timerWidth = ((Frame)main.FindName("frMain")).ActualWidth;
            Invoke(main, "TodoToggle_Click", null, new RoutedEventArgs()); Pump();
            Check(Math.Abs(((Frame)main.FindName("frMain")).ActualWidth - timerWidth) < 0.1,
                "Toggling overlay changed the timer width");
            Invoke(main, "TodoToggle_Click", null, new RoutedEventArgs()); Pump();
            var player = main.Wallpaper.Player;
            foreach (var theme in new[] { AppTheme.Light, AppTheme.Dark })
            {
                ThemeManager.Apply(theme);
                main.Wallpaper.Apply(new()); Pump();
                Render(main, $"{theme}-default.png");
                string image = Path.GetFullPath("Pomodoro/Assets/Images/studyTime.jpg");
                main.Wallpaper.Apply(new(BackgroundKind.Image, image, 0.25, 0.5)); Pump();
                var taskList = (ItemsControl)view.FindName("TaskItems");
                var presenter = (ContentPresenter)taskList.ItemContainerGenerator.ContainerFromIndex(0);
                var title = (TextBlock)presenter.ContentTemplate.FindName("TaskTitle", presenter);
                Check(((SolidColorBrush)title.Foreground).Color == ((SolidColorBrush)view.FindResource("InkBrush")).Color,
                    "Task title does not follow theme");
                var blur = (WallpaperBackdrop)main.FindName("TodoBlurBackground");
                Check(ReferenceEquals(blur.Source, main.FindName("WallpaperViewport")), "Backdrop is not sampling the shared wallpaper viewport");
                Check(ReferenceEquals(player, main.Wallpaper.Player), "Extra background player");
                Check(panel.Clip is StreamGeometry && ((Border)view.Parent).CornerRadius == new CornerRadius(16, 0, 0, 16), "Wrong corner clipping");
                Render(main, $"{theme}-background.png");
            }
            var items = (ItemsControl)view.FindName("TaskItems");
            items.UpdateLayout();
            var container = (ContentPresenter)items.ItemContainerGenerator.ContainerFromIndex(0);
            var expander = FindChild<Expander>(container)!;
            expander.IsExpanded = true; Pump();
            Render(main, "details.png");
            main.Width = 820; main.Height = 700; Pump();
            Check(panel.ActualHeight <= 600 && ((Frame)main.FindName("frMain")).ActualWidth > panel.ActualWidth,
                "Narrow window squeezes timer");
            Render(main, "compact-overlay.png");
            Invoke(main, "TodoToggle_Click", null, new RoutedEventArgs()); Pump();
            Check(panel.Visibility == Visibility.Collapsed, "Hide failed");
            Render(main, "compact-hidden.png");
            Invoke(main, "TodoToggle_Click", null, new RoutedEventArgs()); Pump();
            Check(panel.Visibility == Visibility.Visible && view.Items.Count == 3, "Show lost tasks");
        }
        finally { main.Close(); ThemeManager.Apply(AppTheme.System); }
    }

    private static void MotionAndDrag()
    {
        string path = FreshPath();
        var view = new TodoListView(path) { MotionOverride = true };
        view.AddTask("Một"); view.AddTask("Hai");
        var host = new Window { Content = view, Width = 400, Height = 500, Background = (Brush)view.FindResource("CanvasBrush") };
        host.Show(); Pump();
        try
        {
            var rows = (ItemsControl)view.FindName("TaskItems");
            var scroll = (ScrollViewer)view.FindName("TaskScroll");
            view.AddTask("Ba");
            var added = (FrameworkElement)rows.ItemContainerGenerator.ContainerFromIndex(2);
            Check(added.HasAnimatedProperties && ((TranslateTransform)added.RenderTransform).HasAnimatedProperties,
                "New task has no fade/slide animation");
            Settle();
            var first = view.Items[0];
            var firstRow = (FrameworkElement)rows.ItemContainerGenerator.ContainerFromIndex(0);
            var lastRow = (FrameworkElement)rows.ItemContainerGenerator.ContainerFromIndex(2);
            Point start = firstRow.TranslatePoint(new Point(8, 16), scroll);
            Point finish = lastRow.TranslatePoint(new Point(8, lastRow.ActualHeight - 2), scroll);
            string savedBefore = File.ReadAllText(path);
            Check(view.BeginDragPreview(first, start), "Could not start drag");
            view.UpdateDragPreview(finish);
            Check(view.IsDragging && view.DragDestination == 2 && firstRow.Opacity == 0, "No drag placeholder/target");
            var secondRow = (FrameworkElement)rows.ItemContainerGenerator.ContainerFromIndex(1);
            Check(((TranslateTransform)secondRow.RenderTransform).HasAnimatedProperties, "Other tasks do not slide aside");
            Check(view.Items[0] == first && File.ReadAllText(path) == savedBefore, "Hover changed saved order");
            Render(host, "drag-preview.png");
            view.FinishDragPreview(true); Settle();
            Check(view.Items[2] == first && !view.IsDragging, "Drop did not commit new position");
            Check(new TodoListView(path).Items[2].Title == first.Title, "Drop was not saved");
            var next = view.Items[0];
            firstRow = (FrameworkElement)rows.ItemContainerGenerator.ContainerFromIndex(0);
            lastRow = (FrameworkElement)rows.ItemContainerGenerator.ContainerFromIndex(2);
            start = firstRow.TranslatePoint(new Point(8, 16), scroll);
            finish = lastRow.TranslatePoint(new Point(8, lastRow.ActualHeight - 2), scroll);
            savedBefore = File.ReadAllText(path);
            Check(view.BeginDragPreview(next, start), "Second drag failed");
            view.UpdateDragPreview(finish);
            view.FinishDragPreview(false); Settle();
            Check(view.Items[0] == next && File.ReadAllText(path) == savedBefore && firstRow.Opacity == 1,
                "Cancel changed order or left invisible task");
            view.MotionOverride = false;
            view.AddTask("Không chuyển động");
            added = (FrameworkElement)rows.ItemContainerGenerator.ContainerFromIndex(3);
            Check(!added.HasAnimatedProperties && !((TranslateTransform)added.RenderTransform).HasAnimatedProperties,
                "Reduced motion still animates");
            view.Items[0].IsCompleted = true; Pump();
            Check(rows.Items.Count == 3 && ((ItemsControl)view.FindName("CompletedItems")).Items.Count == 1
                && !((Expander)view.FindName("CompletedSection")).IsExpanded, "Completed section is not separated/collapsed");
        }
        finally { host.Close(); }
    }

    private static void Settle()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(230) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found) return found;
            if (FindChild<T>(child) is { } descendant) return descendant;
        }
        return null;
    }
    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
    private static void Render(Window window, string name)
    {
        window.UpdateLayout();
        var root = (FrameworkElement)window.Content;
        var bitmap = new RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        var backdrop = new DrawingVisual();
        using (var dc = backdrop.RenderOpen()) dc.DrawRectangle(window.Background, null, new Rect(root.RenderSize));
        bitmap.Render(backdrop); bitmap.Render(root);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(Path.Combine(Output, name)); encoder.Save(output);
    }
    private static void Invoke(object target, string method, params object?[] args) => target.GetType()
        .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(target, args);
    private static void Check(bool value, string error) { if (!value) throw new InvalidOperationException(error); }
}
