using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pomodoro.Features.Music;

// One drawing rather than twelve layout animations per frame.
public sealed class SpectrumView : Control
{
    private readonly float[] heights = new float[SpectrumSampleProvider.BandCount];

    internal void Update(float[] target, bool animate)
    {
        bool changed = false;
        for (int i = 0; i < heights.Length; i++)
        {
            float value = animate ? heights[i] + (target[i] - heights[i]) * (target[i] > heights[i] ? 0.65f : 0.22f) : 0;
            changed |= Math.Abs(value - heights[i]) > 0.0001;
            heights[i] = value;
        }
        if (changed) InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double step = ActualWidth / heights.Length;
        double width = Math.Max(1, Math.Min(4, step - 2));
        for (int i = 0; i < heights.Length; i++)
        {
            double height = 3 + heights[i] * Math.Max(0, ActualHeight - 3);
            dc.DrawRoundedRectangle(Foreground, null,
                new Rect(i * step + (step - width) / 2, (ActualHeight - height) / 2, width, height), width / 2, width / 2);
        }
    }
}
