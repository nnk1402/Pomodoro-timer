namespace Pomodoro.Interface;

internal interface ITimerControl
{
    bool IsRunning { get; }
    void pause();
    void resume();
    void update();
}
