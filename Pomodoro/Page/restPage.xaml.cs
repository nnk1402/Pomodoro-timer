namespace Pomodoro;

public partial class restPage : TimerPage
{
    public restPage(Action onComplete) : this(onComplete, true) { }

    public restPage(Action onComplete, bool start)
    {
        InitializeComponent();
        Display.SetLabels("Thả lỏng một chút", "Rời màn hình, uống nước và để mắt được nghỉ.");
        Configure(Display, () => AppSetting.restTime, onComplete, start);
    }
}
