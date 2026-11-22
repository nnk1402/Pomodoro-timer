using Pomodoro.Features.Timer;
using Pomodoro.Features.Settings;
namespace Pomodoro.Features.Timer.Pages;

public partial class LongRestPage : TimerPage
{
    public LongRestPage(Action onComplete) : this(onComplete, true) { }

    public LongRestPage(Action onComplete, bool start)
    {
        InitializeComponent();
        Display.SetLabels("Bạn xứng đáng được nghỉ", "Đứng dậy, giãn cơ và nạp lại năng lượng.");
        Configure(Display, () => AppSetting.longRestTime, onComplete, start);
    }
}
