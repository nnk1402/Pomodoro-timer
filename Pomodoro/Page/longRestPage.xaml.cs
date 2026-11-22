namespace Pomodoro;

public partial class longRestPage : TimerPage
{
    public longRestPage(Action onComplete) : this(onComplete, true) { }

    public longRestPage(Action onComplete, bool start)
    {
        InitializeComponent();
        Display.SetLabels("Bạn xứng đáng được nghỉ", "Đứng dậy, giãn cơ và nạp lại năng lượng.");
        Configure(Display, () => AppSetting.longRestTime, onComplete, start);
    }
}
