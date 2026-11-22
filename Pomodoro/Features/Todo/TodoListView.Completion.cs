using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Pomodoro.Features.Todo;

public partial class TodoListView
{
    private Action? finishCompletion;

    private void FinishCompletionTransition() => finishCompletion?.Invoke();

    private void AnimateCompletion(TodoItem item)
    {
        var before = CapturePositions();
        // The model has changed, but the filtered views still contain the old row.
        var row = RowFor(item);
        Save(updateSummary: false); // Persist immediately, even if the window closes during motion.
        if (!IsLoaded || !MotionEnabled || row is not { IsVisible: true })
        {
            pendingView.Refresh();
            completedView.Refresh();
            UpdateSummary();
            AnimateLayout(before, item);
            return;
        }

        var translation = Translation(row);
        bool wasHitTestVisible = row.IsHitTestVisible;
        row.IsHitTestVisible = false;
        Action complete = () =>
        {
            finishCompletion = null;
            row.BeginAnimation(OpacityProperty, null);
            row.Opacity = 1;
            row.IsHitTestVisible = wasHitTestVisible;
            translation.BeginAnimation(TranslateTransform.XProperty, null);
            translation.X = 0;
            pendingView.Refresh();
            completedView.Refresh();
            UpdateSummary();
            // Let neighbours close the gap and the destination row fade into its new group.
            before.Remove(item);
            AnimateLayout(before, item);
        };
        finishCompletion = complete;

        var duration = TimeSpan.FromMilliseconds(170);
        translation.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(translation.X, item.IsCompleted ? 18 : -18, duration)
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } });
        var fade = new DoubleAnimation(row.Opacity, 0, duration);
        fade.Completed += (_, _) =>
        {
            if (ReferenceEquals(finishCompletion, complete)) complete();
        };
        row.BeginAnimation(OpacityProperty, fade);
    }
}
