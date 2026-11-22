using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Pomodoro.Page;

public partial class Setting : Window
{
    private string? selectedAlarmPath;
    private bool initializing = true;
    private readonly BackgroundController? background;
    private readonly BackgroundOptions originalBackground;
    private BackgroundOptions selectedBackground;
    private readonly string storagePath;

    public Setting() : this(null) { }

    internal Setting(BackgroundController? background, string? storagePath = null)
    {
        this.storagePath = storagePath ?? AppSetting.StoragePath;
        this.background = background;
        originalBackground = background?.Current ?? AppSetting.Background;
        selectedBackground = originalBackground;
        InitializeComponent();
        SourceInitialized += (_, _) => ThemeManager.UpdateTitleBar(this);
        WorkTimeTextBox.Text = AppSetting.workTime.ToString();
        RestTimeTextBox.Text = AppSetting.restTime.ToString();
        LongRestTimeTextBox.Text = AppSetting.longRestTime.ToString();
        selectedAlarmPath = AppSetting.CustomAlarmPath;
        SelectTheme(AppSetting.Theme);
        UpdateAlarmLabel();
        UpdateBackgroundControls();
        initializing = false;
        if (background is not null)
        {
            background.Changed += BackgroundChanged;
            BackgroundChanged();
        }
        Closed += (_, _) =>
        {
            if (background is not null)
            {
                background.Changed -= BackgroundChanged;
                if (DialogResult != true) background.Apply(originalBackground);
            }
            ThemeManager.Apply(AppSetting.Theme);
        };
    }

    private void UpdateBackgroundControls()
    {
        bool previous = initializing;
        initializing = true;
        DefaultBackground.IsChecked = selectedBackground.Kind == BackgroundKind.Default;
        ImageBackground.IsChecked = selectedBackground.Kind == BackgroundKind.Image;
        VideoBackground.IsChecked = selectedBackground.Kind == BackgroundKind.Video;
        BackgroundPathText.Text = selectedBackground.Path ?? "Nền mặc định";
        DarknessSlider.Value = selectedBackground.Darkness * 100;
        SurfaceSlider.Value = selectedBackground.SurfaceOpacity * 100;
        ChooseBackgroundButton.IsEnabled = selectedBackground.Kind != BackgroundKind.Default;
        BackgroundAdjustments.IsEnabled = selectedBackground.Kind != BackgroundKind.Default;
        SaveSettingsButton.IsEnabled = background?.IsLoading != true;
        initializing = previous;
    }

    private void BackgroundChanged()
    {
        if (background is null) return;
        // An unreadable source falls back visibly and can be saved as Default.
        selectedBackground = background.Current;
        UpdateBackgroundControls();
        BackgroundStatus.Text = background.Error ?? (background.IsLoading ? "Đang kiểm tra khả năng phát video…"
            : selectedBackground.Kind == BackgroundKind.Default ? "Đang dùng nền mặc định."
            : "Đang xem trước. Tệp đã mở thành công.");
        BackgroundStatus.SetResourceReference(TextBlock.ForegroundProperty,
            background.Error is null ? "MutedBrush" : "ErrorBrush");
    }

    private void PreviewBackground()
    {
        if (background is not null) background.Apply(selectedBackground);
        else UpdateBackgroundControls();
    }

    private void Background_Checked(object sender, RoutedEventArgs e)
    {
        if (initializing) return;
        var kind = VideoBackground.IsChecked == true ? BackgroundKind.Video
            : ImageBackground.IsChecked == true ? BackgroundKind.Image : BackgroundKind.Default;
        if (kind == BackgroundKind.Default)
        {
            selectedBackground = new BackgroundOptions();
            PreviewBackground();
        }
        else ChooseBackground(kind);
    }

    private void ChooseBackground_Click(object sender, RoutedEventArgs e) => ChooseBackground(selectedBackground.Kind);

    private void ChooseBackground(BackgroundKind kind)
    {
        var dialog = new OpenFileDialog
        {
            Title = kind == BackgroundKind.Image ? "Chọn ảnh nền" : "Chọn video nền",
            Filter = kind == BackgroundKind.Image ? "Ảnh JPG, JPEG, PNG|*.jpg;*.jpeg;*.png"
                : "Video MP4 (ưu tiên)|*.mp4|Video khác|*.wmv;*.avi;*.mov;*.m4v;*.mkv;*.webm|Tất cả tệp|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == true)
        {
            selectedBackground = selectedBackground with { Kind = kind, Path = dialog.FileName };
            PreviewBackground();
        }
        UpdateBackgroundControls();
    }

    private void BackgroundDisplay_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (initializing) return;
        selectedBackground = selectedBackground with { Darkness = DarknessSlider.Value / 100,
            SurfaceOpacity = SurfaceSlider.Value / 100 };
        PreviewBackground();
    }

    private AppTheme SelectedTheme => DarkTheme.IsChecked == true ? AppTheme.Dark
        : LightTheme.IsChecked == true ? AppTheme.Light : AppTheme.System;

    private void SelectTheme(AppTheme theme)
    {
        LightTheme.IsChecked = theme == AppTheme.Light;
        DarkTheme.IsChecked = theme == AppTheme.Dark;
        SystemTheme.IsChecked = theme == AppTheme.System;
    }

    private void Theme_Checked(object sender, RoutedEventArgs e)
    {
        if (!initializing) ThemeManager.Apply(SelectedTheme);
    }

    private void UpdateAlarmLabel() =>
        RingtonePathTextBox.Text = selectedAlarmPath ?? "Chuông mặc định";

    private void ChooseRingtoneButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Tệp âm thanh MP3 (*.mp3)|*.mp3", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        selectedAlarmPath = dialog.FileName;
        UpdateAlarmLabel();
        ErrorText.Visibility = Visibility.Collapsed;
    }

    private void setDefaultButton_Click(object sender, RoutedEventArgs e)
    {
        // Stage defaults in the dialog. Cancel must leave the running app unchanged.
        WorkTimeTextBox.Text = "25";
        RestTimeTextBox.Text = "5";
        LongRestTimeTextBox.Text = "15";
        selectedAlarmPath = null;
        SelectTheme(AppTheme.System);
        selectedBackground = new BackgroundOptions();
        PreviewBackground();
        UpdateAlarmLabel();
        ErrorText.Visibility = Visibility.Collapsed;
    }

    private bool ReadMinutes(TextBox input, out int minutes)
    {
        if (int.TryParse(input.Text, out minutes) && AppSetting.ValidMinutes(minutes)) return true;
        ShowError("Thời lượng cần là số nguyên từ 1 đến 180 phút.");
        input.Focus();
        input.SelectAll();
        return false;
    }

    private void ShowError(string error)
    {
        ErrorText.Text = error;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (background?.IsLoading == true) return;
        if (!ReadMinutes(WorkTimeTextBox, out int work) ||
            !ReadMinutes(RestTimeTextBox, out int rest) ||
            !ReadMinutes(LongRestTimeTextBox, out int longRest)) return;
        if (!AppSetting.Save(new SettingsData(work, rest, longRest, selectedAlarmPath, SelectedTheme, selectedBackground), storagePath, out string error))
        {
            ShowError(error);
            return;
        }
        DialogResult = true;
    }
}
