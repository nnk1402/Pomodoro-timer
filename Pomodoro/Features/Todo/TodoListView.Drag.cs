using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Pomodoro.Features.Todo;

public partial class TodoListView
{
    private Point gripStart;
    private TodoItem? gripItem;
    private FrameworkElement? capturedGrip;
    private TodoItem? draggedItem;
    private ItemsControl? dragGroup;
    private List<TodoItem> dragOrder = [];
    private readonly Dictionary<TodoItem, Rect> dragBounds = [];
    private DragGhost? dragGhost;
    private AdornerLayer? dragLayer;
    private Point grabOffset;
    private Point lastDragPointer;
    private int dragDestination;
    private readonly DispatcherTimer dragScrollTimer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    internal bool IsDragging => draggedItem is not null;
    internal int DragDestination => dragDestination;

    private void Grip_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: TodoItem item } grip) return;
        FinishDragPreview(false);
        gripStart = e.GetPosition(TaskScroll);
        gripItem = item;
        capturedGrip = grip;
        grip.Focus(); grip.CaptureMouse();
        e.Handled = true;
    }

    private void Grip_MouseMove(object sender, MouseEventArgs e)
    {
        if (gripItem is null || e.LeftButton != MouseButtonState.Pressed) return;
        Point now = e.GetPosition(TaskScroll);
        if (!IsDragging && Math.Abs(now.X - gripStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(now.Y - gripStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        if (!IsDragging && !BeginDragPreview(gripItem, gripStart)) return;
        UpdateDragPreview(now);
        e.Handled = true;
    }

    private void Grip_MouseUp(object sender, MouseButtonEventArgs e)
    {
        Point point = e.GetPosition(TaskScroll);
        FinishDragPreview(point.X >= 0 && point.X <= TaskScroll.ActualWidth && point.Y >= 0 && point.Y <= TaskScroll.ActualHeight);
        ReleaseGrip();
        e.Handled = true;
    }

    private void Grip_LostCapture(object sender, MouseEventArgs e)
    {
        FinishDragPreview(false);
        gripItem = null; capturedGrip = null;
    }

    private void Grip_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && (IsDragging || gripItem is not null))
        {
            FinishDragPreview(false); ReleaseGrip(); e.Handled = true; return;
        }
        if (Keyboard.Modifiers != ModifierKeys.Alt || sender is not FrameworkElement { DataContext: TodoItem item }) return;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is not (Key.Up or Key.Down)) return;
        var group = GroupFor(item).Items.Cast<TodoItem>().ToList();
        int index = group.IndexOf(item), destination = index + (key == Key.Up ? -1 : 1);
        if (destination >= 0 && destination < group.Count) DropTask(item, group[destination], destination > index);
        e.Handled = true;
    }

    internal bool BeginDragPreview(TodoItem item, Point pointer)
    {
        if (!IsLoaded || !Items.Contains(item)) return false;
        FinishDragPreview(false);
        FinishCompletionTransition();
        ResetTransforms(); UpdateLayout();
        if (RowFor(item) is not { IsVisible: true } row) return false;
        dragGroup = GroupFor(item);
        dragOrder = dragGroup.Items.Cast<TodoItem>().ToList();
        dragBounds.Clear();
        foreach (var entry in dragOrder)
            if (RowFor(entry) is { } container)
                dragBounds[entry] = new Rect(container.TranslatePoint(new Point(), dragGroup), container.RenderSize);
        if (!dragBounds.ContainsKey(item)) return false;
        Point origin = row.TranslatePoint(new Point(), TaskScroll);
        grabOffset = pointer - (Vector)origin;
        // Snapshot the card once; video/blur is never captured or decoded again.
        var dpi = VisualTreeHelper.GetDpi(row);
        var snapshot = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(row.ActualWidth * dpi.DpiScaleX)),
            Math.Max(1, (int)Math.Ceiling(row.ActualHeight * dpi.DpiScaleY)), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen()) dc.DrawRectangle(new VisualBrush(row) { Stretch = Stretch.Fill }, null, new Rect(row.RenderSize));
        snapshot.Render(visual); snapshot.Freeze();
        var surface = ((Brush)FindResource("SurfaceBrush")).CloneCurrentValue(); surface.Opacity = 1;
        dragGhost = new DragGhost(TaskScroll, snapshot, row.RenderSize, surface, (Brush)FindResource("LineBrush"));
        dragLayer = AdornerLayer.GetAdornerLayer(TaskScroll);
        dragLayer?.Add(dragGhost);
        draggedItem = item;
        dragDestination = dragOrder.IndexOf(item);
        row.BeginAnimation(OpacityProperty, null); row.Opacity = 0;
        dragScrollTimer.Tick -= DragScroll_Tick;
        dragScrollTimer.Tick += DragScroll_Tick;
        dragScrollTimer.Start();
        UpdateDragPreview(pointer);
        return true;
    }

    internal void UpdateDragPreview(Point pointer)
    {
        if (draggedItem is null || dragGroup is null || dragGhost is null) return;
        lastDragPointer = pointer;
        Point inGroup = TaskScroll.TranslatePoint(pointer, dragGroup);
        double center = inGroup.Y - grabOffset.Y + dragBounds[draggedItem].Height / 2;
        dragDestination = dragOrder.Count(item => item != draggedItem && dragBounds.TryGetValue(item, out Rect bounds)
            && center > bounds.Top + bounds.Height / 2);
        int source = dragOrder.IndexOf(draggedItem);
        double height = dragBounds[draggedItem].Height;
        for (int i = 0; i < dragOrder.Count; i++)
        {
            var item = dragOrder[i];
            if (item == draggedItem || RowFor(item) is not { } row) continue;
            double offset = i > source && i <= dragDestination ? -height
                : i >= dragDestination && i < source ? height : 0;
            var transform = Translation(row);
            // Do not restart an in-flight animation for an unchanged destination.
            if (Math.Abs((double)transform.GetAnimationBaseValue(TranslateTransform.YProperty) - offset) > 0.1)
                Transition(transform, TranslateTransform.YProperty, offset);
        }
        dragGhost.Position = new Point(Math.Clamp(pointer.X - grabOffset.X, -6, 6), pointer.Y - grabOffset.Y);
        dragGhost.InvalidateVisual();
    }

    private void DragScroll_Tick(object? sender, EventArgs e)
    {
        if (draggedItem is null) { dragScrollTimer.Stop(); return; }
        double y = lastDragPointer.Y;
        if (y < 28) TaskScroll.ScrollToVerticalOffset(TaskScroll.VerticalOffset - 14);
        else if (y > TaskScroll.ActualHeight - 28) TaskScroll.ScrollToVerticalOffset(TaskScroll.VerticalOffset + 14);
        else return;
        TaskScroll.UpdateLayout();
        UpdateDragPreview(lastDragPointer);
    }

    internal void FinishDragPreview(bool commit)
    {
        if (draggedItem is null) return;
        var item = draggedItem;
        Point released = dragGhost?.Position ?? new Point();
        var before = CapturePositions();
        var others = dragOrder.Where(entry => entry != item).ToList();
        int destination = Math.Clamp(dragDestination, 0, others.Count);
        dragScrollTimer.Stop();
        dragScrollTimer.Tick -= DragScroll_Tick;
        if (dragGhost is not null) dragLayer?.Remove(dragGhost);
        dragGhost = null; dragLayer = null;
        draggedItem = null;
        if (RowFor(item) is { } previous) previous.Opacity = 1;
        // Commit only on mouse-up. Escape/capture loss never touches persisted order.
        if (commit && others.Count > 0)
        {
            var target = destination == others.Count ? others[^1] : others[destination];
            DropTask(item, target, destination == others.Count);
        }
        before[item] = released;
        AnimateLayout(before);
        foreach (var entry in Items)
            if (RowFor(entry) is { } row && entry != item && !before.ContainsKey(entry))
                Transition(Translation(row), TranslateTransform.YProperty, 0);
        dragBounds.Clear(); dragOrder.Clear(); dragGroup = null;
        ReleaseGrip();
    }

    private void ReleaseGrip()
    {
        var grip = capturedGrip;
        capturedGrip = null; gripItem = null;
        grip?.ReleaseMouseCapture();
    }

    private sealed class DragGhost(UIElement viewport, ImageSource snapshot, Size size, Brush surface, Brush line) : Adorner(viewport)
    {
        internal Point Position { get; set; }
        protected override void OnRender(DrawingContext dc)
        {
            IsHitTestVisible = false;
            dc.PushClip(new RectangleGeometry(new Rect(AdornedElement.RenderSize)));
            var bounds = new Rect(Position, size);
            dc.DrawRoundedRectangle(surface, new Pen(line, 1), bounds, 8, 8);
            dc.DrawImage(snapshot, bounds);
            dc.Pop();
        }
    }
}
