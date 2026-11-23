using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Pomodoro.Features.Music;

public sealed class MarqueeText : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(MarqueeText), new PropertyMetadata("", Changed));
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    private readonly TextBlock label = new();
    private readonly TranslateTransform offset = new();
    private readonly Canvas viewport = new() { ClipToBounds = true };

    public MarqueeText()
    {
        IsHitTestVisible = false;
        label.RenderTransform = offset;
        label.SetBinding(TextBlock.TextProperty, new Binding(nameof(Text)) { Source = this });
        label.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(Foreground)) { Source = this });
        label.SetBinding(TextBlock.FontSizeProperty, new Binding(nameof(FontSize)) { Source = this });
        label.SetBinding(TextBlock.FontWeightProperty, new Binding(nameof(FontWeight)) { Source = this });
        viewport.Children.Add(label);
        Content = viewport;
        SizeChanged += (_, _) => Restart();
        Loaded += (_, _) => { SystemParameters.StaticPropertyChanged += MotionChanged; Restart(); };
        Unloaded += (_, _) => { SystemParameters.StaticPropertyChanged -= MotionChanged; offset.BeginAnimation(TranslateTransform.XProperty, null); };
    }

    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e) => ((MarqueeText)sender).Restart();
    private void MotionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.ClientAreaAnimation)) Dispatcher.BeginInvoke(Restart);
    }

    private void Restart()
    {
        offset.BeginAnimation(TranslateTransform.XProperty, null);
        offset.X = 0;
        if (!IsLoaded || !SystemParameters.ClientAreaAnimation || ActualWidth <= 0) return;
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double width = label.DesiredSize.Width;
        if (width <= ActualWidth) return;
        offset.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(-width, ActualWidth, TimeSpan.FromSeconds((width + ActualWidth) / 30))
            { BeginTime = TimeSpan.FromSeconds(1.2), RepeatBehavior = RepeatBehavior.Forever });
    }
}
