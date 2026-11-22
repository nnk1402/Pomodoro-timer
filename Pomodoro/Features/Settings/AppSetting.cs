using Pomodoro.Features.Background;
using Pomodoro.Themes;
using Pomodoro.Services;
using System.IO;
using System.Text.Json;

namespace Pomodoro.Features.Settings;

internal static class AppSetting
{
    public static int workTime { get; private set; } = 25;
    public static int restTime { get; private set; } = 5;
    public static int longRestTime { get; private set; } = 15;
    public static string? CustomAlarmPath { get; private set; }
    public static AppTheme Theme { get; private set; } = AppTheme.System;
    public static BackgroundOptions Background { get; private set; } = new();
    internal static string StoragePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pomodoro", "settings.json");

    public static bool ValidMinutes(int value) => value is >= 1 and <= 180;
    public static bool IsValid(SettingsData data) => ValidMinutes(data.WorkMinutes) && ValidMinutes(data.RestMinutes) && ValidMinutes(data.LongRestMinutes) && Enum.IsDefined(data.Theme) && (data.Background?.IsValid ?? true);

    public static void Load()
    {
        try
        {
            if (!File.Exists(StoragePath)) return;
            var data = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(StoragePath));
            if (data is not null && IsValid(data)) Apply(data);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            // A missing or damaged preferences file must not prevent startup.
        }
    }

    internal static void Apply(SettingsData data)
    {
        workTime = data.WorkMinutes;
        restTime = data.RestMinutes;
        longRestTime = data.LongRestMinutes;
        CustomAlarmPath = data.AlarmPath;
        Theme = data.Theme;
        Background = data.Background is { IsValid: true } background ? background : new();
        ThemeManager.Apply(data.Theme);
        if (!string.IsNullOrWhiteSpace(data.AlarmPath) && File.Exists(data.AlarmPath)) Alarm.changeAlarm(data.AlarmPath);
        else Alarm.setDefaultAlarm();
    }

    public static bool Save(SettingsData data, out string error) => Save(data, StoragePath, out error);

    internal static bool Save(SettingsData data, string destination, out string error)
    {
        error = "";
        if (!IsValid(data))
        {
            error = "Mỗi thời lượng cần là số nguyên từ 1 đến 180 phút.";
            return false;
        }
        if (!string.IsNullOrWhiteSpace(data.AlarmPath) && !File.Exists(data.AlarmPath))
        {
            error = "Không tìm thấy tệp nhạc chuông. Hãy chọn lại hoặc dùng chuông mặc định.";
            return false;
        }
        if (data.Background is { Kind: not BackgroundKind.Default } selected && !File.Exists(selected.Path))
        {
            error = "Không tìm thấy tệp hình nền. Hãy chọn lại hoặc trở về nền mặc định.";
            return false;
        }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            string temporary = destination + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, destination, true);
            Apply(data);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = "Chưa thể lưu cài đặt. Hãy kiểm tra quyền ghi hoặc dung lượng ổ đĩa rồi thử lại.";
            return false;
        }
    }
}
