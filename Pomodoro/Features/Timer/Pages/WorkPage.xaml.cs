using Pomodoro.Features.Timer;
using Pomodoro.Features.Settings;
namespace Pomodoro.Features.Timer.Pages;

public partial class WorkPage : TimerPage
{
    public WorkPage(Action onComplete) : this(onComplete, true) { }

    public WorkPage(Action onComplete, bool start)
    {
        InitializeComponent();
        Display.SetLabels("Tập trung vào một việc", "Một khoảng thời gian nhỏ. Một bước tiến rõ ràng.");
        Configure(Display, () => AppSetting.workTime, onComplete, start);
    }
}
