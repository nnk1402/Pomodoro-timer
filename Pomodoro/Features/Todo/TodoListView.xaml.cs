using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace Pomodoro.Features.Todo;

public partial class TodoListView : UserControl
{
    private readonly string storagePath;
    private readonly DispatcherTimer saveDelay = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private readonly ListCollectionView pendingView;
    private readonly ListCollectionView completedView;
    internal ObservableCollection<TodoItem> Items { get; } = [];
    public event EventHandler? CloseRequested;

    public TodoListView() : this(TodoStore.StoragePath) { }

    internal TodoListView(string storagePath)
    {
        this.storagePath = storagePath;
        InitializeComponent();
        pendingView = new ListCollectionView(Items) { Filter = value => !((TodoItem)value).IsCompleted };
        completedView = new ListCollectionView(Items) { Filter = value => ((TodoItem)value).IsCompleted };
        TaskItems.ItemsSource = pendingView;
        CompletedItems.ItemsSource = completedView;
        saveDelay.Tick += (_, _) => Flush();
        ConfigureMotion();
        var saved = TodoStore.Load(storagePath, out string error);
        foreach (var data in saved)
        {
            var item = new TodoItem(data);
            item.PropertyChanged += ItemChanged;
            Items.Add(item);
        }
        ShowSaveError(error);
        UpdateSummary();
    }

    internal bool AddTask(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return false;
        FinishDragPreview(false);
        FinishCompletionTransition();
        var before = CapturePositions();
        var item = new TodoItem(new TodoData(text));
        item.PropertyChanged += ItemChanged;
        Items.Add(item);
        Save();
        AnimateLayout(before, item);
        return true;
    }

    internal void DeleteTask(TodoItem item)
    {
        FinishDragPreview(false);
        FinishCompletionTransition();
        var before = CapturePositions();
        if (!Items.Remove(item)) return;
        item.PropertyChanged -= ItemChanged;
        Save();
        AnimateLayout(before);
    }

    private void ItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TodoItem.Details))
        {
            saveDelay.Stop(); saveDelay.Start();
            return;
        }
        FinishDragPreview(false);
        FinishCompletionTransition();
        if (sender is TodoItem item) AnimateCompletion(item);
    }

    internal void Flush()
    {
        FinishDragPreview(false);
        FinishCompletionTransition();
        if (saveDelay.IsEnabled) Save();
    }

    internal void MoveTask(TodoItem item, int destination)
    {
        FinishCompletionTransition();
        int source = Items.IndexOf(item);
        if (source < 0 || Items.Count < 2) return;
        destination = Math.Clamp(destination, 0, Items.Count - 1);
        if (source == destination) return;
        var before = CapturePositions();
        Items.Move(source, destination);
        Save();
        AnimateLayout(before);
    }

    internal void DropTask(TodoItem item, TodoItem target, bool after)
    {
        if (!Items.Contains(target)) return;
        int insert = Items.IndexOf(target) + (after ? 1 : 0);
        if (Items.IndexOf(item) < insert) insert--;
        MoveTask(item, insert);
    }

    private void UpdateSummary()
    {
        int done = Items.Count(item => item.IsCompleted);
        SummaryText.Text = Items.Count == 0 ? "Dành chỗ cho điều quan trọng." : $"{Items.Count - done} việc cần làm";
        CompletedCount.Text = $"{(CompletedSection.IsExpanded ? "▾" : "▸")}  Đã hoàn thành ({done})";
        CompletedSection.Visibility = done == 0 ? Visibility.Collapsed : Visibility.Visible;
        EmptyState.Visibility = Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Save(bool updateSummary = true)
    {
        saveDelay.Stop();
        TodoStore.Save(Items.Select(item => new TodoData(item.Title, item.IsCompleted, item.Details)), storagePath, out string error);
        ShowSaveError(error);
        if (updateSummary) UpdateSummary();
    }

    private void ShowSaveError(string error)
    {
        SaveError.Text = error;
        SaveError.Visibility = RetryButton.Visibility = error.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (AddTask(DraftText.Text))
        {
            DraftText.Clear();
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                UpdateLayout();
                if (pendingView.Cast<TodoItem>().LastOrDefault() is { } last) RowFor(last)?.BringIntoView();
            }));
        }
        DraftText.Focus();
    }

    private void Draft_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        Add_Click(sender, new RoutedEventArgs());
    }

    private void Draft_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (AddButton is null) return;
        AddButton.IsEnabled = !string.IsNullOrWhiteSpace(DraftText.Text);
        AddButton.Visibility = AddButton.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TodoItem item }) DeleteTask(item);
    }

    private void Retry_Click(object sender, RoutedEventArgs e) => Save();
    private void Close_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
}
