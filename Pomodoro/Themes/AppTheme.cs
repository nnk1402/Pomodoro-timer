using System.Text.Json.Serialization;

namespace Pomodoro.Themes;

[JsonConverter(typeof(JsonStringEnumConverter<AppTheme>))]
public enum AppTheme { System, Light, Dark }

