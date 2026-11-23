using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Pomodoro.Features.Music;

public partial class MusicPlayerView : UserControl, IDisposable
{
    public static readonly DependencyProperty WallpaperSourceProperty = DependencyProperty.Register(
        nameof(WallpaperSource), typeof(FrameworkElement), typeof(MusicPlayerView));
    public FrameworkElement? WallpaperSource
    {
        get => (FrameworkElement?)GetValue(WallpaperSourceProperty);
        set => SetValue(WallpaperSourceProperty, value);
    }

    private readonly MusicPlayback playback;
    private readonly string storagePath;
    private readonly MusicLibraryData library;
    private readonly bool canSave;
    private readonly DispatcherTimer frames = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly DispatcherTimer fileChecks = new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(15) };
    private readonly float[] levels = new float[SpectrumSampleProvider.BandCount];
    private MusicPlaylist? playingPlaylist;
    private MusicTrack? current;
    private bool checking;
    private bool disposed;
    private string notice = "";
    private readonly HashSet<SpectrumView> rowSpectra = [];
    private MusicPlaylist SelectedPlaylist { get; }

    public MusicPlayerView() : this(MusicStore.StoragePath) { }

    internal MusicPlayerView(string path, MusicPlayback? player = null)
    {
        storagePath = path;
        playback = player ?? new MusicPlayback();
        library = MusicStore.Load(path, out string error);
        canSave = string.IsNullOrEmpty(error);
        InitializeComponent();
        SelectedPlaylist = library.Playlists.FirstOrDefault(p => p.Id == library.SelectedPlaylistId) ?? library.Playlists[0];
        current = library.Playlists.SelectMany(p => p.Tracks).FirstOrDefault(t => string.Equals(t.FilePath, library.CurrentTrackPath, StringComparison.OrdinalIgnoreCase))
            ?? SelectedPlaylist.Tracks.FirstOrDefault();
        playingPlaylist = library.Playlists.FirstOrDefault(p => current is not null && p.Tracks.Contains(current)) ?? SelectedPlaylist;
        UpdateModeButtons();
        if (!canSave)
        {
            AddMusicButton.IsEnabled = false;
            ShowNotice(error);
        }
        playback.Stopped += Playback_Stopped;
        frames.Tick += Frame_Tick;
        fileChecks.Tick += async (_, _) => await CheckFilesAsync();
        Loaded += async (_, _) =>
        {
            fileChecks.Start();
            SystemParameters.StaticPropertyChanged += SystemParameters_Changed;
            await CheckFilesAsync();
            UpdatePlayer();
            RestartMarquee();
        };
        Unloaded += (_, _) =>
        {
            FinishReorder(false);
            ReleaseGrip();
            frames.Stop();
            fileChecks.Stop();
            SystemParameters.StaticPropertyChanged -= SystemParameters_Changed;
            TitleOffset.BeginAnimation(TranslateTransform.XProperty, null);
            LibraryPopup.IsOpen = false;
        };
        UpdatePlayer();
    }

    private void ShowNotice(string message)
    {
        notice = message;
        LibraryNotice.Text = message;
        LibraryNotice.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
        MiniPlayer.ToolTip = string.IsNullOrEmpty(message) ? current?.FilePath ?? "Thêm MP3 để nghe trong lúc tập trung" : message;
    }

    private void Save()
    {
        if (!canSave) return;
        library.SelectedPlaylistId = SelectedPlaylist.Id;
        library.CurrentTrackPath = current?.FilePath;
        if (!MusicStore.Save(library, storagePath, out string error)) ShowNotice(error);
    }

    private async Task CheckFilesAsync()
    {
        if (checking || disposed || !canSave) return;
        checking = true;
        try
        {
            string[] paths = library.Playlists.SelectMany(p => p.Tracks).Select(t => t.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            string[] missing = await Task.Run(() => paths.Where(p => !MusicStore.CanRead(p)).ToArray());
            if (disposed || missing.Length == 0) return;
            FinishReorder(false);
            ReleaseGrip();
            if (MusicStore.Prune(library, missing) == 0) return;
            if (current is not null && missing.Contains(current.FilePath, StringComparer.OrdinalIgnoreCase))
            {
                playback.Stop();
                current = null;
                UpdatePlayer();
            }
            ShowNotice($"Đã bỏ {missing.Length} file không còn truy cập được khỏi danh sách.");
            RefreshTracks();
            Save();
        }
        finally { checking = false; }
    }

    private void RefreshTracks()
    {
        TrackList.ItemsSource = SelectedPlaylist.Tracks;
        TrackCount.Text = $"{SelectedPlaylist.Tracks.Count} bài nhạc";
        EmptyNotice.Visibility = SelectedPlaylist.Tracks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddMusic_Click(object sender, RoutedEventArgs e)
    {
        if (!canSave) return;
        var picker = new OpenFileDialog { Title = "Thêm MP3 vào Music list", Filter = "Nhạc MP3 (*.mp3)|*.mp3", Multiselect = true, CheckFileExists = true };
        LibraryPopup.IsOpen = false;
        if (picker.ShowDialog(Window.GetWindow(this)) == true) ImportFiles(picker.FileNames);
        LibraryPopup.IsOpen = true;
    }

    internal void ImportFiles(IEnumerable<string> paths)
    {
        if (!canSave) return;
        var positions = CaptureRows(TrackList);
        int failed = 0;
        var existing = new HashSet<string>(SelectedPlaylist.Tracks.Select(t => t.FilePath), StringComparer.OrdinalIgnoreCase);
        foreach (string path in paths)
        {
            string full;
            try { full = Path.GetFullPath(path); }
            catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { failed++; continue; }
            if (!string.Equals(Path.GetExtension(full), ".mp3", StringComparison.OrdinalIgnoreCase) || !MusicStore.CanRead(full)) { failed++; continue; }
            if (existing.Add(full)) SelectedPlaylist.Tracks.Add(new MusicTrack { FilePath = full });
        }
        if (current is null)
        {
            current = SelectedPlaylist.Tracks.FirstOrDefault();
            playingPlaylist = SelectedPlaylist;
        }
        ShowNotice(failed > 0 ? $"Không thêm được {failed} file. Chỉ chọn MP3 còn truy cập được." : "");
        RefreshTracks();
        AnimateRows(TrackList, positions);
        UpdatePlayer();
        Save();
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (playback.IsPlaying) { playback.Pause(); UpdatePlayer(); return; }
        if (current is null)
        {
            current = SelectedPlaylist.Tracks.FirstOrDefault();
            playingPlaylist = SelectedPlaylist;
        }
        if (current is null) { LibraryPopup.IsOpen = true; return; }
        PlayTrack(current, playingPlaylist ?? SelectedPlaylist);
    }

    private void Track_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: MusicTrack track }) ToggleTrack(track);
    }

    private void ToggleTrack(MusicTrack track)
    {
        if (ReferenceEquals(current, track) && playback.IsPlaying) { playback.Pause(); UpdatePlayer(); }
        else PlayTrack(track, SelectedPlaylist);
    }

    private void PlayMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: MusicPlayMode mode }) return;
        library.PlayMode = mode;
        UpdateModeButtons(true);
        Save();
    }

    private void UpdateModeButtons(bool animate = false)
    {
        foreach (var button in new[] { RepeatOneButton, RepeatAllButton, ShuffleButton })
        {
            bool active = (MusicPlayMode)button.Tag == library.PlayMode;
            button.SetResourceReference(ForegroundProperty, active ? "AccentInkBrush" : "MutedBrush");
            System.Windows.Automation.AutomationProperties.SetItemStatus(button, active ? "Đang chọn" : "Chưa chọn");
        }
        UpdatePlaybackModeHighlight(animate);
    }

    private bool playbackModePositioned;
    private double playbackModeTarget = double.NaN;

    private void PlaybackModeNavigation_SizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdatePlaybackModeHighlight(false);

    private void UpdatePlaybackModeHighlight(bool animate)
    {
        if (PlaybackModeNavigation is null || !PlaybackModeNavigation.IsVisible) return;
        var selected = library.PlayMode switch
        {
            MusicPlayMode.RepeatOne => RepeatOneButton,
            MusicPlayMode.Shuffle => ShuffleButton,
            _ => RepeatAllButton
        };
        if (selected.ActualWidth <= 0 || selected.ActualHeight <= 0) return;
        double target = selected.TranslatePoint(new Point(), PlaybackModeNavigation).X;
        PlaybackModeHighlight.Width = selected.ActualWidth;
        PlaybackModeHighlight.Height = selected.ActualHeight;
        PlaybackModeHighlight.Visibility = Visibility.Visible;
        if (animate && playbackModePositioned && Math.Abs(target - playbackModeTarget) < 0.1) return;
        // Retarget from the currently displayed offset when the user clicks rapidly.
        double from = PlaybackModeOffset.X;
        PlaybackModeOffset.BeginAnimation(TranslateTransform.XProperty, null);
        PlaybackModeOffset.X = target;
        if (animate && playbackModePositioned && SystemParameters.ClientAreaAnimation
            && !SystemParameters.HighContrast && Math.Abs(from - target) > 0.1)
        {
            PlaybackModeOffset.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(from, target, TimeSpan.FromMilliseconds(200))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.Stop
                });
        }
        playbackModePositioned = true;
        playbackModeTarget = target;
    }

    internal void PlayTrack(MusicTrack track, MusicPlaylist playlist)
    {
        if (!MusicStore.CanRead(track.FilePath))
        {
            MusicStore.Prune(library, [track.FilePath]);
            if (current?.FilePath == track.FilePath) { playback.Stop(); current = null; }
            ShowNotice("File không còn truy cập được nên đã bỏ khỏi danh sách.");
            RefreshTracks();
            UpdatePlayer();
            Save();
            return;
        }
        current = track;
        playingPlaylist = playlist;
        try
        {
            playback.Play(track.FilePath);
            ShowNotice("");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // An invalid decoder/device is not evidence that the source file vanished.
            ShowNotice("Chưa phát được bài này. Kiểm tra file MP3 và thiết bị âm thanh rồi thử lại.");
            LibraryPopup.IsOpen = true;
            _ = CheckFilesAsync();
        }
        UpdatePlayer();
        Save();
    }

    private void Playback_Stopped(Exception? error)
    {
        string? stoppedPath = playback.FilePath;
        int version = playback.Version;
        Dispatcher.BeginInvoke(() =>
        {
            if (disposed || playback.IsPlaying || version != playback.Version || stoppedPath != playback.FilePath) return;
            playback.Stop();
            if (error is not null)
            {
                ShowNotice("Phát nhạc bị gián đoạn. Kiểm tra file và thiết bị âm thanh rồi thử lại.");
                UpdatePlayer();
                _ = CheckFilesAsync();
                return;
            }
            var playlist = playingPlaylist;
            if (playlist is null || playlist.Tracks.Count == 0) { UpdatePlayer(); return; }
            var next = MusicSequence.Next(playlist, current, library.PlayMode);
            // Missing paths cannot interrupt the remaining accessible playlist.
            while (next is not null && !MusicStore.CanRead(next.FilePath))
            {
                int index = playlist.Tracks.IndexOf(next);
                MusicStore.Prune(library, [next.FilePath]);
                next = library.PlayMode == MusicPlayMode.Shuffle
                    ? MusicSequence.Next(playlist, current, library.PlayMode)
                    : playlist.Tracks.Count > 0 ? playlist.Tracks[index % playlist.Tracks.Count] : null;
            }
            RefreshTracks();
            if (next is not null) PlayTrack(next, playlist);
            else { current = null; UpdatePlayer(); Save(); }
        });
    }

    private void RemoveTrack_Click(object sender, RoutedEventArgs e)
    {
        if (!canSave || sender is not Button { Tag: MusicTrack track }) return;
        FinishReorder(false);
        var positions = CaptureRows(TrackList);
        if (ReferenceEquals(track, current)) { playback.Stop(); current = null; UpdatePlayer(); }
        SelectedPlaylist.Tracks.Remove(track);
        RefreshTracks();
        AnimateRows(TrackList, positions);
        Save();
    }

    private void TrackList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (FindParent<Button>(e.OriginalSource as DependencyObject) is not null) return;
        if (e.Key != Key.Enter || TrackList.SelectedItem is not MusicTrack track) return;
        e.Handled = true;
        ToggleTrack(track);
    }

    private void UpdatePlayer()
    {
        string title = current?.DisplayName ?? "Thêm nhạc";
        if (TrackTitle.Text != title) { TrackTitle.Text = title; RestartMarquee(); }
        bool playing = playback.IsPlaying;
        foreach (var track in library.Playlists.SelectMany(p => p.Tracks))
        {
            track.IsCurrent = ReferenceEquals(track, current);
            track.IsPlaying = track.IsCurrent && playing;
        }
        PlayIcon.Data = Geometry.Parse(playing ? "M 0,0 L 4,0 L 4,14 L 0,14 Z M 8,0 L 12,0 L 12,14 L 8,14 Z" : "M 0,0 L 0,14 L 11,7 Z");
        string label = playing ? "Tạm dừng nhạc" : "Phát nhạc";
        PlayButton.ToolTip = label;
        System.Windows.Automation.AutomationProperties.SetName(PlayButton, label);
        if (playing && IsLoaded && SystemParameters.ClientAreaAnimation) frames.Start();
        else
        {
            frames.Stop();
            Array.Clear(levels);
            Spectrum.Update(levels, false);
        }
        UpdateRowSpectra();
        MiniPlayer.ToolTip = string.IsNullOrEmpty(notice) ? current?.FilePath ?? "Thêm MP3 để nghe trong lúc tập trung" : notice;
    }

    private void Frame_Tick(object? sender, EventArgs e)
    {
        playback.CopyLevels(levels);
        Spectrum.Update(levels, SystemParameters.ClientAreaAnimation);
        UpdateRowSpectra();
    }

    private void RowSpectrum_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is SpectrumView spectrum) { rowSpectra.Add(spectrum); UpdateRowSpectra(); }
    }
    private void RowSpectrum_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is SpectrumView spectrum) rowSpectra.Remove(spectrum);
    }
    private readonly float[] silence = new float[SpectrumSampleProvider.BandCount];
    private void UpdateRowSpectra()
    {
        foreach (var spectrum in rowSpectra)
        {
            bool active = ReferenceEquals(spectrum.DataContext, current) && playback.IsPlaying;
            spectrum.Update(active ? levels : silence, active && SystemParameters.ClientAreaAnimation);
        }
    }

    private void RestartMarquee()
    {
        TitleOffset.BeginAnimation(TranslateTransform.XProperty, null);
        TitleOffset.X = 0;
        if (!IsLoaded || current is null || !SystemParameters.ClientAreaAnimation) return;
        TrackTitle.Measure(new Size(double.PositiveInfinity, 20));
        double width = TrackTitle.DesiredSize.Width;
        double viewport = TitleViewport.ActualWidth;
        if (viewport <= 0) return;
        // Enter from the left and travel completely out on the right.
        var animation = new DoubleAnimation(-width, viewport, TimeSpan.FromSeconds((viewport + width) / 28))
        { RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromSeconds(1) };
        TitleOffset.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    private void TitleViewport_SizeChanged(object sender, SizeChangedEventArgs e) => RestartMarquee();
    private void SystemParameters_Changed(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(SystemParameters.ClientAreaAnimation) or nameof(SystemParameters.HighContrast))) return;
        Dispatcher.BeginInvoke(() => { UpdatePlayer(); RestartMarquee(); UpdatePlaybackModeHighlight(false); });
    }

    private void MiniPlayer_SizeChanged(object sender, SizeChangedEventArgs e) =>
        MiniPlayer.Clip = new RectangleGeometry(new Rect(e.NewSize), 12, 12);
    private void Library_Click(object sender, RoutedEventArgs e) => LibraryPopup.IsOpen = !LibraryPopup.IsOpen;
    private async void Library_Opened(object? sender, EventArgs e)
    {
        LibraryPanel.MaxHeight = Math.Max(200, SystemParameters.WorkArea.Height - 32);
        PlaybackModeNavigation.UpdateLayout();
        UpdatePlaybackModeHighlight(false);
        RefreshTracks();
        TrackList.Focus();
        LibraryPanel.BeginAnimation(OpacityProperty, null);
        if (SystemParameters.ClientAreaAnimation)
            LibraryPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
        await CheckFilesAsync();
    }
    private void Library_Closed(object? sender, EventArgs e)
    {
        PlaybackModeOffset.BeginAnimation(TranslateTransform.XProperty, null);
        playbackModePositioned = false;
        FinishReorder(false);
        ReleaseGrip();
        LibraryButton.Focus();
    }
    private void Library_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            if (reorderItem is not null || capturedGrip is not null) { FinishReorder(false); ReleaseGrip(); }
            else LibraryPopup.IsOpen = false;
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        FinishReorder(false);
        ReleaseGrip();
        frames.Stop();
        fileChecks.Stop();
        SystemParameters.StaticPropertyChanged -= SystemParameters_Changed;
        TitleOffset.BeginAnimation(TranslateTransform.XProperty, null);
        LibraryPopup.IsOpen = false;
        playback.Stopped -= Playback_Stopped;
        playback.Dispose();
        Save();
    }
}
