using Pomodoro.Features.Background;
using Pomodoro.Themes;

namespace Pomodoro.Features.Settings;

internal sealed record SettingsData(int WorkMinutes = 25, int RestMinutes = 5, int LongRestMinutes = 15, string? AlarmPath = null, AppTheme Theme = AppTheme.System, BackgroundOptions? Background = null);
