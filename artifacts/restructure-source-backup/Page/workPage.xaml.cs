namespace Pomodoro;

public partial class workPage : TimerPage
{
    public workPage(Action onComplete) : this(onComplete, true) { }

    public workPage(Action onComplete, bool start)
    {
        InitializeComponent();
        Display.SetLabels("Tập trung vào một việc", "Một khoảng thời gian nhỏ. Một bước tiến rõ ràng.");
        Configure(Display, () => AppSetting.workTime, onComplete, start);
    }
}
