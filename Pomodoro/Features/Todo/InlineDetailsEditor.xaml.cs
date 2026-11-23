using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Pomodoro.Features.Todo;

public partial class InlineDetailsEditor : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(InlineDetailsEditor),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, TextChanged));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public bool IsEditing { get; private set; }
    private bool resizePending;
    private bool contextMenuOpen;
    private bool MotionEnabled => SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;

    public InlineDetailsEditor()
    {
        InitializeComponent();
        UpdateHint();
        Loaded += (_, _) => SystemParameters.StaticPropertyChanged += MotionChanged;
        Unloaded += (_, _) =>
        {
            SystemParameters.StaticPropertyChanged -= MotionChanged;
            ResetMotion();
        };
        SizeChanged += (_, e) =>
        {
            if (e.WidthChanged) QueueResize();
        };
    }

    private static void TextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var control = (InlineDetailsEditor)sender;
        if (control.Hint is null) return;
        control.UpdateHint();
        control.QueueResize();
    }

    private void UpdateHint()
    {
        bool empty = string.IsNullOrEmpty(Text);
        Hint.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        DetailsPreview.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        double previous = RevealHost.ActualHeight;
        IsEditing = true;
        Preview.Visibility = Visibility.Collapsed;
        Editor.Visibility = Visibility.Visible;
        ResizeToContent(previous);
        FadeIn(Editor);
        Editor.Focus();
        Editor.CaretIndex = Editor.Text.Length;
    }

    private void FinishEditing(bool returnFocus)
    {
        if (!IsEditing) return;
        double previous = RevealHost.ActualHeight;
        // Keep the binding intact when normalising an empty note.
        if (string.IsNullOrWhiteSpace(Text)) SetCurrentValue(TextProperty, "");
        IsEditing = false;
        Editor.Visibility = Visibility.Collapsed;
        Preview.Visibility = Visibility.Visible;
        UpdateHint();
        ResizeToContent(previous);
        FadeIn(Preview);
        if (returnFocus) Preview.Focus();
    }

    private void Editor_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!contextMenuOpen) FinishEditing(false);
    }

    private void Editor_ContextMenuOpening(object sender, ContextMenuEventArgs e) => contextMenuOpen = true;

    private void Editor_ContextMenuClosing(object sender, ContextMenuEventArgs e)
    {
        contextMenuOpen = false;
        Dispatcher.BeginInvoke(() =>
        {
            if (!Editor.IsKeyboardFocusWithin) FinishEditing(false);
        });
    }

    private void Editor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape && !(e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)) return;
        e.Handled = true;
        FinishEditing(true);
    }

    private void Editor_TextChanged(object sender, TextChangedEventArgs e) => QueueResize();

    private void QueueResize()
    {
        if (!IsLoaded || resizePending) return;
        resizePending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            resizePending = false;
            if (IsLoaded) ResizeToContent(RevealHost.ActualHeight);
        }));
    }

    private void ResizeToContent(double previous)
    {
        Body.Measure(new Size(Math.Max(0, ActualWidth), double.PositiveInfinity));
        double target = Math.Max(22, Body.DesiredSize.Height);
        RevealHost.BeginAnimation(HeightProperty, null);
        RevealHost.Height = double.NaN;
        if (!MotionEnabled || previous <= 0 || Math.Abs(previous - target) < 0.5) return;
        var animation = new DoubleAnimation(previous, target, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };
        RevealHost.BeginAnimation(HeightProperty, animation);
    }

    private void FadeIn(FrameworkElement element)
    {
        element.BeginAnimation(OpacityProperty, null);
        if (!MotionEnabled) return;
        element.BeginAnimation(OpacityProperty, new DoubleAnimation(0.4, 1, TimeSpan.FromMilliseconds(160))
        { FillBehavior = FillBehavior.Stop });
    }

    private void MotionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SystemParameters.ClientAreaAnimation) or nameof(SystemParameters.HighContrast))
            Dispatcher.BeginInvoke(() => { if (!MotionEnabled) ResetMotion(); });
    }

    private void ResetMotion()
    {
        RevealHost.BeginAnimation(HeightProperty, null);
        RevealHost.Height = double.NaN;
        Preview.BeginAnimation(OpacityProperty, null);
        Editor.BeginAnimation(OpacityProperty, null);
    }
}
