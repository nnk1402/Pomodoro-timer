using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Pomodoro.Features.Music;

public partial class MusicPlayerView
{
    private FrameworkElement? capturedGrip;
    private Action? pendingRowClick;
    private ListBox? reorderList;
    private object? reorderItem;
    private Point gripStart;
    private Point lastPointer;
    private double grabOffset;
    private int reorderDestination;
    private readonly DispatcherTimer reorderScroll = new() { Interval = TimeSpan.FromMilliseconds(40) };
    internal bool IsReordering => reorderItem is not null;
    internal int ReorderDestination => reorderDestination;

    private void Row_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!canSave || sender is not FrameworkElement grip || FindParent<ListBox>(grip) is not { } list) return;
        FinishReorder(false);
        ReleaseGrip();
        capturedGrip = grip;
        // Defer button clicks until release so dragging anywhere, including a
        // play/trash button, never also plays or deletes a track.
        if (FindParent<Button>(e.OriginalSource as DependencyObject) is { } button)
            pendingRowClick = () => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        else if (grip.DataContext is MusicTrack track)
            pendingRowClick = () => ToggleTrack(track);
        reorderList = list;
        gripStart = e.GetPosition(list);
        list.SelectedItem = grip.DataContext;
        grip.Focus();
        grip.CaptureMouse();
        e.Handled = true;
    }

    private void Row_MouseMove(object sender, MouseEventArgs e)
    {
        if (capturedGrip is null || reorderList is null || e.LeftButton != MouseButtonState.Pressed) return;
        var pointer = e.GetPosition(reorderList);
        if (reorderItem is null)
        {
            if (Math.Abs(pointer.X - gripStart.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(pointer.Y - gripStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            if (!BeginReorder(reorderList, capturedGrip.DataContext, gripStart)) return;
        }
        UpdateReorder(pointer);
        e.Handled = true;
    }

    private void Row_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (reorderList is null) return;
        var pointer = e.GetPosition(reorderList);
        var click = !IsReordering && capturedGrip is { } row
            && new Rect(row.RenderSize).Contains(e.GetPosition(row)) ? pendingRowClick : null;
        FinishReorder(new Rect(reorderList.RenderSize).Contains(pointer));
        ReleaseGrip();
        click?.Invoke();
        e.Handled = true;
    }

    private void Row_LostCapture(object sender, MouseEventArgs e)
    {
        // Captures lost by a descendant button must not cancel the row's capture.
        if (!ReferenceEquals(e.OriginalSource, sender)) return;
        FinishReorder(false);
        capturedGrip = null;
        reorderList = null;
        pendingRowClick = null;
    }

    private void Row_KeyDown(object sender, KeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is not (Key.Up or Key.Down) || Keyboard.Modifiers != ModifierKeys.Alt
            || sender is not FrameworkElement grip || FindParent<ListBox>(grip) is not { } list) return;
        int index = list.Items.IndexOf(grip.DataContext);
        MoveItem(list, grip.DataContext, index + (key == Key.Up ? -1 : 1));
        e.Handled = true;
    }

    internal bool BeginReorder(ListBox list, object item, Point pointer)
    {
        if (!canSave || !list.IsVisible || !list.Items.Contains(item)) return false;
        // End animations before calculating the stable starting row positions.
        ResetRows(list);
        list.UpdateLayout();
        if (list.ItemContainerGenerator.ContainerFromItem(item) is not FrameworkElement row) return false;
        reorderList = list;
        reorderItem = item;
        reorderDestination = list.Items.IndexOf(item);
        grabOffset = pointer.Y - row.TranslatePoint(new Point(), list).Y;
        row.Opacity = 0.85;
        Panel.SetZIndex(row, 10);
        reorderScroll.Tick -= ReorderScroll_Tick;
        reorderScroll.Tick += ReorderScroll_Tick;
        reorderScroll.Start();
        UpdateReorder(pointer);
        return true;
    }

    internal void UpdateReorder(Point pointer)
    {
        if (reorderList is not { } list || reorderItem is not { } item
            || list.ItemContainerGenerator.ContainerFromItem(item) is not FrameworkElement dragged) return;
        lastPointer = pointer;
        double center = pointer.Y - grabOffset + dragged.ActualHeight / 2;
        var bounds = new Dictionary<object, double>();
        foreach (var entry in list.Items)
            if (list.ItemContainerGenerator.ContainerFromItem(entry) is FrameworkElement row)
                bounds[entry] = row.TranslatePoint(new Point(), list).Y - Translation(row).Y;
        if (!bounds.TryGetValue(item, out double origin)) return;
        reorderDestination = list.Items.Cast<object>().Count(entry => !ReferenceEquals(entry, item)
            && bounds.TryGetValue(entry, out double top) && center > top + RowHeight(list, entry) / 2);
        int source = list.Items.IndexOf(item);
        double slotHeight = dragged.ActualHeight + dragged.Margin.Top + dragged.Margin.Bottom;
        for (int i = 0; i < list.Items.Count; i++)
        {
            var entry = list.Items[i];
            if (ReferenceEquals(entry, item) || list.ItemContainerGenerator.ContainerFromItem(entry) is not FrameworkElement row) continue;
            double offset = i > source && i <= reorderDestination ? -slotHeight
                : i >= reorderDestination && i < source ? slotHeight : 0;
            var transform = Translation(row);
            if (Math.Abs((double)transform.GetAnimationBaseValue(TranslateTransform.YProperty) - offset) > 0.1)
                Transition(transform, offset);
        }
        var movement = Translation(dragged);
        movement.BeginAnimation(TranslateTransform.YProperty, null);
        movement.Y = pointer.Y - grabOffset - origin;
    }

    internal void FinishReorder(bool commit)
    {
        reorderScroll.Stop();
        if (reorderList is not { } list || reorderItem is not { } item) return;
        var before = CaptureRows(list);
        int destination = reorderDestination;
        reorderItem = null;
        ResetRows(list);
        if (commit) MoveItemCore(list, item, destination);
        AnimateRows(list, before);
        if (commit) Save();
    }

    internal bool MoveItem(ListBox list, object item, int destination)
    {
        if (!canSave || destination < 0 || destination >= list.Items.Count) return false;
        var before = CaptureRows(list);
        if (!MoveItemCore(list, item, destination)) return false;
        AnimateRows(list, before);
        Save();
        return true;
    }

    private bool MoveItemCore(ListBox list, object item, int destination)
    {
        int source = list.Items.IndexOf(item);
        if (source < 0 || destination < 0 || destination >= list.Items.Count || source == destination) return false;
        object selected = list.SelectedItem;
        if (ReferenceEquals(list, TrackList)) SelectedPlaylist.Tracks.Move(source, destination);
        else return false;
        list.SelectedItem = selected;
        return true;
    }

    private void ReorderScroll_Tick(object? sender, EventArgs e)
    {
        if (reorderList is not { } list || reorderItem is null) { reorderScroll.Stop(); return; }
        if (FindChild<ScrollViewer>(list) is not { } scroll) return;
        if (lastPointer.Y < 28) scroll.ScrollToVerticalOffset(scroll.VerticalOffset - 12);
        else if (lastPointer.Y > list.ActualHeight - 28) scroll.ScrollToVerticalOffset(scroll.VerticalOffset + 12);
        else return;
        list.UpdateLayout();
        UpdateReorder(lastPointer);
    }

    private void ReleaseGrip()
    {
        var grip = capturedGrip;
        capturedGrip = null;
        reorderList = null;
        pendingRowClick = null;
        grip?.ReleaseMouseCapture();
    }

    private static double RowHeight(ListBox list, object item) =>
        (list.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement)?.ActualHeight ?? 0;

    private static Dictionary<object, double> CaptureRows(ListBox list)
    {
        var positions = new Dictionary<object, double>();
        if (!list.IsLoaded) return positions;
        foreach (var item in list.Items)
            if (list.ItemContainerGenerator.ContainerFromItem(item) is FrameworkElement row)
                positions[item] = row.TranslatePoint(new Point(), list).Y;
        return positions;
    }

    private static TranslateTransform Translation(FrameworkElement row)
    {
        if (row.RenderTransform is TranslateTransform transform) return transform;
        var translation = new TranslateTransform();
        row.RenderTransform = translation;
        return translation;
    }

    private static void Transition(TranslateTransform transform, double to, double? from = null)
    {
        double start = from ?? transform.Y;
        transform.BeginAnimation(TranslateTransform.YProperty, null);
        transform.Y = to;
        if (!SystemParameters.ClientAreaAnimation || Math.Abs(start - to) < 0.1) return;
        transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(start, to, TimeSpan.FromMilliseconds(180))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
    }

    private static void AnimateRows(ListBox list, Dictionary<object, double> before)
    {
        if (!list.IsLoaded) return;
        list.UpdateLayout();
        foreach (var item in list.Items)
        {
            if (list.ItemContainerGenerator.ContainerFromItem(item) is not FrameworkElement row) continue;
            var transform = Translation(row);
            double layout = row.TranslatePoint(new Point(), list).Y - transform.Y;
            Transition(transform, 0, before.TryGetValue(item, out double old) ? old - layout : 8);
            if (!before.ContainsKey(item) && SystemParameters.ClientAreaAnimation)
                row.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)) { FillBehavior = FillBehavior.Stop });
        }
    }

    private static void ResetRows(ListBox list)
    {
        foreach (var item in list.Items)
        {
            if (list.ItemContainerGenerator.ContainerFromItem(item) is not FrameworkElement row) continue;
            Translation(row).BeginAnimation(TranslateTransform.YProperty, null);
            Translation(row).Y = 0;
            row.BeginAnimation(OpacityProperty, null);
            row.Opacity = 1;
            Panel.SetZIndex(row, 0);
        }
    }

    private static T? FindParent<T>(DependencyObject? item) where T : DependencyObject
    {
        while (item is not null)
        {
            if (item is T target) return target;
            item = VisualTreeHelper.GetParent(item);
        }
        return null;
    }

    private static T? FindChild<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) return match;
            if (FindChild<T>(child) is { } descendant) return descendant;
        }
        return null;
    }
}
