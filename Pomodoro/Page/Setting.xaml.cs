using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Pomodoro.Page;

public partial class Setting : Window
{
    private string? selectedAlarmPath;
    private bool initializing = true;

    public Setting()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => ThemeManager.UpdateTitleBar(this);
        WorkTimeTextBox.Text = AppSetting.workTime.ToString();
        RestTimeTextBox.Text = AppSetting.restTime.ToString();
        LongRestTimeTextBox.Text = AppSetting.longRestTime.ToString();
        selectedAlarmPath = AppSetting.CustomAlarmPath;
        SelectTheme(AppSetting.Theme);
        UpdateAlarmLabel();
        initializing = false;
        Closed += (_, _) => ThemeManager.Apply(AppSetting.Theme);
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
        if (!ReadMinutes(WorkTimeTextBox, out int work) ||
            !ReadMinutes(RestTimeTextBox, out int rest) ||
            !ReadMinutes(LongRestTimeTextBox, out int longRest)) return;
        if (!AppSetting.Save(new SettingsData(work, rest, longRest, selectedAlarmPath, SelectedTheme), out string error))
        {
            ShowError(error);
            return;
        }
        DialogResult = true;
    }
}
