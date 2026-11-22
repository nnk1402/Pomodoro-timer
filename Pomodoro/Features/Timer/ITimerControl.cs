namespace Pomodoro.Features.Timer;

internal interface ITimerControl
{
    bool IsRunning { get; }
    void pause();
    void resume();
    void update();
}
