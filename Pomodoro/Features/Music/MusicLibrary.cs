using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.ComponentModel;

namespace Pomodoro.Features.Music;

public sealed class MusicTrack : INotifyPropertyChanged
{
    private bool isPlaying;
    private bool isCurrent;
    public string FilePath { get; set; } = "";
    [JsonIgnore] public string DisplayName => Path.GetFileName(FilePath);
    [JsonIgnore] public bool IsPlaying
    {
        get => isPlaying;
        internal set { if (isPlaying == value) return; isPlaying = value; PropertyChanged?.Invoke(this, new(nameof(IsPlaying))); }
    }
    [JsonIgnore] public bool IsCurrent
    {
        get => isCurrent;
        internal set { if (isCurrent == value) return; isCurrent = value; PropertyChanged?.Invoke(this, new(nameof(IsCurrent))); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public enum MusicPlayMode { RepeatAll, RepeatOne, Shuffle }

internal static class MusicSequence
{
    internal static MusicTrack? Next(MusicPlaylist playlist, MusicTrack? current, MusicPlayMode mode, Random? random = null)
    {
        if (playlist.Tracks.Count == 0) return null;
        int index = current is null ? -1 : playlist.Tracks.IndexOf(current);
        if (mode == MusicPlayMode.RepeatOne && index >= 0) return current;
        if (mode == MusicPlayMode.Shuffle && playlist.Tracks.Count > 1)
        {
            int next = (random ?? Random.Shared).Next(playlist.Tracks.Count - (index >= 0 ? 1 : 0));
            return playlist.Tracks[index >= 0 && next >= index ? next + 1 : next];
        }
        return playlist.Tracks[(index + 1) % playlist.Tracks.Count];
    }
}

public sealed class MusicPlaylist
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public ObservableCollection<MusicTrack> Tracks { get; set; } = [];
}

internal sealed class MusicLibraryData
{
    public ObservableCollection<MusicPlaylist> Playlists { get; set; } = [];
    public Guid SelectedPlaylistId { get; set; }
    public string? CurrentTrackPath { get; set; }
    public MusicPlayMode PlayMode { get; set; } = MusicPlayMode.RepeatAll;
}

internal static class MusicStore
{
    internal static string StoragePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pomodoro", "music.json");

    internal static MusicLibraryData Load(string path, out string error)
    {
        error = "";
        try
        {
            if (!File.Exists(path)) return CreateDefault();
            var data = JsonSerializer.Deserialize<MusicLibraryData>(File.ReadAllText(path));
            if (data?.Playlists is null || data.Playlists.Any(p => p is null
                || string.IsNullOrWhiteSpace(p.Name) || p.Tracks is null
                || p.Tracks.Any(t => t is null || string.IsNullOrWhiteSpace(t.FilePath))))
                throw new JsonException();
            var ids = new HashSet<Guid>();
            foreach (var playlist in data.Playlists)
            {
                if (playlist.Id == Guid.Empty || !ids.Add(playlist.Id))
                {
                    playlist.Id = Guid.NewGuid();
                    ids.Add(playlist.Id);
                }
                var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var track in playlist.Tracks.ToArray())
                {
                    // Stored entries must remain local absolute MP3 paths.
                    if (!Path.IsPathFullyQualified(track.FilePath)
                        || !string.Equals(Path.GetExtension(track.FilePath), ".mp3", StringComparison.OrdinalIgnoreCase)
                        || !paths.Add(track.FilePath)) playlist.Tracks.Remove(track);
                }
            }
            if (data.Playlists.Count == 0) return CreateDefault();
            if (!Enum.IsDefined(data.PlayMode)) data.PlayMode = MusicPlayMode.RepeatAll;
            return data;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            error = "Chưa đọc được music.json. Hãy kiểm tra tệp này rồi mở lại ứng dụng; danh sách cũ được giữ nguyên.";
            return CreateDefault();
        }
    }

    private static MusicLibraryData CreateDefault()
    {
        var playlist = new MusicPlaylist { Name = "Nhạc của tôi" };
        return new MusicLibraryData { Playlists = [playlist], SelectedPlaylistId = playlist.Id };
    }

    internal static bool CanRead(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    internal static int Prune(MusicLibraryData data, IEnumerable<string> unavailable)
    {
        var paths = new HashSet<string>(unavailable, StringComparer.OrdinalIgnoreCase);
        int removed = 0;
        foreach (var playlist in data.Playlists)
            foreach (var track in playlist.Tracks.Where(t => paths.Contains(t.FilePath)).ToArray())
            {
                playlist.Tracks.Remove(track);
                removed++;
            }
        if (data.CurrentTrackPath is { } current && paths.Contains(current)) data.CurrentTrackPath = null;
        return removed;
    }

    internal static bool Save(MusicLibraryData data, string path, out string error)
    {
        error = "";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            error = "Chưa lưu được playlist. Kiểm tra quyền ghi hoặc dung lượng; ứng dụng sẽ thử lại khi đóng.";
            return false;
        }
    }
}
