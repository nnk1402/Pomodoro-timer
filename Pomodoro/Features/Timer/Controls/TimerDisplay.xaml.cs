using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pomodoro.Features.Timer.Controls;

public partial class TimerDisplay : UserControl
{
    public static readonly RoutedEvent ToggleRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(ToggleRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TimerDisplay));
    public static readonly RoutedEvent ResetRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(ResetRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TimerDisplay));

    public event RoutedEventHandler ToggleRequested
    {
        add => AddHandler(ToggleRequestedEvent, value);
        remove => RemoveHandler(ToggleRequestedEvent, value);
    }

    public event RoutedEventHandler ResetRequested
    {
        add => AddHandler(ResetRequestedEvent, value);
        remove => RemoveHandler(ResetRequestedEvent, value);
    }

    public TimerDisplay() => InitializeComponent();

    private void Toggle_Click(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(ToggleRequestedEvent, this));
    private void Reset_Click(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(ResetRequestedEvent, this));

    public void SetNextSession(string text) => NextText.Text = text;

    public void SetLabels(string caption, string detail)
    {
        CaptionText.Text = caption;
        DetailText.Text = detail;
    }

    public void Update(TimeSpan remaining, TimeSpan duration, bool running, bool ready = false)
    {
        long seconds = (long)Math.Ceiling(Math.Max(0, remaining.TotalSeconds));
        TimeText.Text = $"{seconds / 60:00}:{seconds % 60:00}";
        StateText.Text = running ? "Đang đếm ngược" : ready ? "Sẵn sàng bắt đầu" : "Đã tạm dừng";
        ControlTimeButton.Content = running ? "Ⅱ   Tạm dừng" : ready ? "▶   Bắt đầu" : "▶   Tiếp tục";
        double progress = duration.TotalSeconds <= 0 ? 0 : Math.Clamp(1 - remaining.TotalSeconds / duration.TotalSeconds, 0, 1);
        if (progress <= 0) { ProgressArc.Data = Geometry.Empty; return; }
        // Keep a tiny gap at a full circle so the arc remains well-defined.
        double angle = Math.Min(progress, 0.99999) * Math.PI * 2;
        var end = new Point(155 + 146.5 * Math.Sin(angle), 155 - 146.5 * Math.Cos(angle));
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(155, 8.5), false, false);
            context.ArcTo(end, new Size(146.5, 146.5), 0, angle > Math.PI, SweepDirection.Clockwise, true, false);
        }
        geometry.Freeze();
        ProgressArc.Data = geometry;
    }
}
