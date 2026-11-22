using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Pomodoro.Features.Todo;

public partial class TodoListView
{
    internal bool? MotionOverride { get; set; }
    internal bool MotionEnabled => MotionOverride ?? (SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast);
    private bool listeningForMotion;

    private void ConfigureMotion()
    {
        Loaded += (_, _) =>
        {
            if (listeningForMotion) return;
            listeningForMotion = true;
            SystemParameters.StaticPropertyChanged += MotionPreferenceChanged;
        };
        Unloaded += (_, _) =>
        {
            Flush();
            ResetTransforms();
            if (!listeningForMotion) return;
            listeningForMotion = false;
            SystemParameters.StaticPropertyChanged -= MotionPreferenceChanged;
        };
        CompletedSection.Expanded += (_, _) => UpdateSummary();
        CompletedSection.Collapsed += (_, _) => UpdateSummary();
    }

    private void MotionPreferenceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (Dispatcher.HasShutdownStarted) return;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (MotionEnabled) return;
            FinishCompletionTransition();
            ResetTransforms();
            if (draggedItem is not null) UpdateDragPreview(lastDragPointer);
        }));
    }

    private ItemsControl GroupFor(TodoItem item) => item.IsCompleted ? CompletedItems : TaskItems;
    private FrameworkElement? RowFor(TodoItem item) =>
        TaskItems.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement ??
        CompletedItems.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;

    private Dictionary<TodoItem, Point> CapturePositions()
    {
        var positions = new Dictionary<TodoItem, Point>();
        if (!IsLoaded) return positions;
        foreach (var item in Items)
            if (RowFor(item) is { IsVisible: true } row) positions[item] = row.TranslatePoint(new Point(), TaskScroll);
        return positions;
    }

    private static TranslateTransform Translation(FrameworkElement row)
    {
        if (row.RenderTransform is TranslateTransform transform) return transform;
        var translation = new TranslateTransform();
        row.RenderTransform = translation;
        return translation;
    }

    private void Transition(TranslateTransform transform, DependencyProperty property, double value, double? from = null)
    {
        double start = from ?? (double)transform.GetValue(property);
        transform.BeginAnimation(property, null);
        transform.SetValue(property, value);
        if (!MotionEnabled || Math.Abs(start - value) < 0.1) return;
        transform.BeginAnimation(property, new DoubleAnimation(start, value, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop
        });
    }

    private void AnimateLayout(Dictionary<TodoItem, Point> before, TodoItem? added = null)
    {
        if (!IsLoaded) return;
        UpdateLayout();
        foreach (var item in Items)
        {
            if (item == draggedItem || RowFor(item) is not { IsVisible: true } row) continue;
            var transform = Translation(row);
            // Measure the new layout position without its previous presentation offset.
            Point current = row.TranslatePoint(new Point(), TaskScroll);
            Point layout = new(current.X - transform.X, current.Y - transform.Y);
            if (before.TryGetValue(item, out Point old))
            {
                Transition(transform, TranslateTransform.XProperty, 0, old.X - layout.X);
                Transition(transform, TranslateTransform.YProperty, 0, old.Y - layout.Y);
            }
            else if (item == added && MotionEnabled)
            {
                Transition(transform, TranslateTransform.YProperty, 0, 12);
                row.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)) { FillBehavior = FillBehavior.Stop });
            }
        }
    }

    private void ResetTransforms()
    {
        foreach (var item in Items)
        {
            if (RowFor(item) is not { } row) continue;
            row.BeginAnimation(OpacityProperty, null);
            row.Opacity = item == draggedItem ? 0 : 1;
            var transform = Translation(row);
            transform.BeginAnimation(TranslateTransform.XProperty, null);
            transform.BeginAnimation(TranslateTransform.YProperty, null);
            transform.X = transform.Y = 0;
        }
    }
}
