using Pomodoro.Features.Timer;
using Pomodoro.Features.Settings;
namespace Pomodoro.Features.Timer.Pages;

public partial class RestPage : TimerPage
{
    public RestPage(Action onComplete) : this(onComplete, true) { }

    public RestPage(Action onComplete, bool start)
    {
        InitializeComponent();
        Display.SetLabels("Thả lỏng một chút", "Rời màn hình, uống nước và để mắt được nghỉ.");
        Configure(Display, () => AppSetting.restTime, onComplete, start);
    }
}
