using System.ComponentModel;

namespace Pomodoro.Features.Todo;

internal sealed record TodoData(string Title, bool IsCompleted = false, string Details = "");

internal sealed class TodoItem : INotifyPropertyChanged
{
    public string Title { get; }
    private string details;
    public string Details
    {
        get => details;
        set
        {
            value ??= "";
            if (details == value) return;
            details = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Details)));
        }
    }
    private bool isCompleted;
    public bool IsCompleted
    {
        get => isCompleted;
        set
        {
            if (isCompleted == value) return;
            isCompleted = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCompleted)));
        }
    }

    public TodoItem(TodoData data) { Title = data.Title; isCompleted = data.IsCompleted; details = data.Details ?? ""; }
    public event PropertyChangedEventHandler? PropertyChanged;
}
