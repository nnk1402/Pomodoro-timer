using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Pomodoro.Features.Background;

// Samples the already rendered wallpaper viewport, including its crop and shade.
// Each panel blurs only its own region; no extra media player or frame capture.
public sealed class WallpaperBackdrop : FrameworkElement
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(FrameworkElement), typeof(WallpaperBackdrop),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, SourceChanged));

    public static readonly DependencyProperty BlurRadiusProperty = DependencyProperty.Register(
        nameof(BlurRadius), typeof(double), typeof(WallpaperBackdrop),
        new FrameworkPropertyMetadata(16d, FrameworkPropertyMetadataOptions.AffectsRender, RadiusChanged),
        value => value is double radius && double.IsFinite(radius) && radius is >= 0 and <= 64);

    private readonly VisualBrush sample = new()
    {
        AutoLayoutContent = false,
        ViewboxUnits = BrushMappingMode.Absolute,
        Stretch = Stretch.Fill,
        AlignmentX = AlignmentX.Left,
        AlignmentY = AlignmentY.Top
    };
    private readonly BlurEffect blur = new() { Radius = 16, RenderingBias = RenderingBias.Performance };
    private Rect sampleBounds = Rect.Empty;
    private bool listening;

    public FrameworkElement? Source
    {
        get => (FrameworkElement?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public double BlurRadius
    {
        get => (double)GetValue(BlurRadiusProperty);
        set => SetValue(BlurRadiusProperty, value);
    }

    public WallpaperBackdrop()
    {
        IsHitTestVisible = false;
        Effect = blur;
        Loaded += (_, _) =>
        {
            sample.Visual = Source;
            if (!listening) { LayoutUpdated += LayoutFinished; listening = true; }
            UpdateSample();
        };
        Unloaded += (_, _) =>
        {
            LayoutUpdated -= LayoutFinished;
            listening = false;
            sample.Visual = null;
            sampleBounds = Rect.Empty;
        };
    }

    private static void SourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var backdrop = (WallpaperBackdrop)sender;
        backdrop.sample.Visual = e.NewValue as FrameworkElement;
        backdrop.sampleBounds = Rect.Empty;
        backdrop.UpdateSample();
    }

    private static void RadiusChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var backdrop = (WallpaperBackdrop)sender;
        backdrop.blur.Radius = (double)e.NewValue;
        backdrop.sampleBounds = Rect.Empty;
        backdrop.UpdateSample();
    }

    private void LayoutFinished(object? sender, EventArgs e) => UpdateSample();

    private Rect PaintBounds
    {
        get
        {
            // Include neighbouring pixels before blurring, then let the panel's
            // rounded clip trim the result. This avoids seams along panel edges.
            double bleed = Math.Ceiling(BlurRadius * 2);
            return new Rect(-bleed, -bleed, ActualWidth + bleed * 2, ActualHeight + bleed * 2);
        }
    }

    private void UpdateSample()
    {
        if (Source is not { IsArrangeValid: true } source || !IsArrangeValid
            || ActualWidth <= 0 || ActualHeight <= 0 || !IsVisible) return;
        Rect bounds;
        try
        {
            // Resolve the final, shared coordinate system after layout, rather
            // than moving a full-size Image using stale SizeChanged offsets.
            bounds = TransformToVisual(source).TransformBounds(PaintBounds);
        }
        catch (InvalidOperationException)
        {
            // Source and panel may briefly be disconnected while loading/unloading.
            return;
        }
        if (bounds == sampleBounds) return;
        sampleBounds = bounds;
        sample.Viewbox = bounds;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        UpdateSample();
        if (sample.Visual is null || sampleBounds.IsEmpty || ActualWidth <= 0 || ActualHeight <= 0) return;
        drawingContext.DrawRectangle(sample, null, PaintBounds);
    }
}
