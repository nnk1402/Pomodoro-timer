using System.IO;
using System.Windows.Media;

namespace Pomodoro.Services;

internal static class Alarm
{
    public static readonly MediaPlayer _player = new();
    public static readonly string defaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Sounds", "alarm_sound.mp3");
    public static string path = defaultPath;

    public static void changeAlarm(string file) => path = file;
    public static void setDefaultAlarm() => path = defaultPath;

    public static bool playAlarm()
    {
        try
        {
            if (!File.Exists(path)) return false;
            _player.Open(new Uri(path, UriKind.Absolute));
            _player.Play();
            return true;
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return false;
        }
    }
}
