using NAudio.Wave;
using Pomodoro.Features.Background;
using Pomodoro.Features.Music;
using Pomodoro.Themes;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

internal static class Program
{
    private static readonly string Output = Path.GetFullPath("artifacts/music-tests");
    private static string Mp3 => Path.Combine(Output, "Một bài nhạc có tên rất dài để kiểm tra chữ chạy từ trái sang phải.mp3");

    [STAThread]
    private static int Main()
    {
        Directory.CreateDirectory(Output);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Assets/Sounds/alarm_sound.mp3"), Mp3, true);
        _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var tests = new (string, Action)[]
        {
            ("Playlist persistence preserves original paths and Unicode", Persistence),
            ("Inaccessible sources are pruned across playlists without deleting files", Pruning),
            ("Corrupt storage is reported and preserved", CorruptStorage),
            ("FFT follows real PCM frequency, amplitude and silence", Spectrum),
            ("MP3 decoding, pause/resume and disposal release the source", Playback),
            ("Playlist UI imports, deduplicates, plays, loops and persists", Interaction),
            ("Repeat-one, repeat-all and shuffle select and persist correctly", PlayModes),
            ("Drag preview, cancel, commit and animation preserve playback", Reordering),
            ("Only the playing row receives PCM spectrum and pause clears all rows", RowSpectrum),
            ("Plus icons are centred and marquee moves from left to right", AlignmentAndMarquee),
            ("Light/dark player, popup, wallpaper blur and minimum shell render", Render),
        };
        int failures = 0;
        foreach (var (name, test) in tests)
        {
            try { test(); Console.WriteLine("PASS: " + name); }
            catch (Exception e) { failures++; Console.WriteLine("FAIL: " + name + "\n" + e.GetBaseException()); }
        }
        Application.Current.Shutdown();
        Console.WriteLine($"{tests.Length - failures}/{tests.Length} passed");
        return failures == 0 ? 0 : 1;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Persistence()
    {
        string path = Path.Combine(Output, "persistence.json");
        var data = new MusicLibraryData();
        var first = new MusicPlaylist { Name = "Tập trung 🎵", Tracks = [new MusicTrack { FilePath = Mp3 }] };
        var second = new MusicPlaylist { Name = "Nghỉ ngơi" };
        data.Playlists.Add(first);
        data.Playlists.Add(second);
        data.SelectedPlaylistId = second.Id;
        data.CurrentTrackPath = Mp3;
        Assert(MusicStore.Save(data, path, out _), "Save failed");
        var loaded = MusicStore.Load(path, out string error);
        Assert(error == "" && loaded.Playlists.Count == 2, "Playlist round trip failed");
        Assert(loaded.Playlists[0].Tracks[0].FilePath == Mp3 && loaded.CurrentTrackPath == Mp3, "Original path changed");
        Assert(loaded.Playlists[0].Name == first.Name && loaded.SelectedPlaylistId == second.Id, "Selection or Unicode changed");
        Assert(!Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp").Any(), "Temporary file not replaced");
        string invalid = Path.Combine(Output, "relative.json");
        first.Tracks.Add(new MusicTrack { FilePath = "relative.mp3" });
        first.Tracks.Add(new MusicTrack { FilePath = Mp3.ToUpperInvariant() });
        MusicStore.Save(data, invalid, out _);
        Assert(MusicStore.Load(invalid, out _).Playlists[0].Tracks.Count == 1, "Invalid paths or duplicates survived reload");
    }

    private static void Pruning()
    {
        string missing = Path.Combine(Output, "not-present.mp3");
        string locked = Path.Combine(Output, "locked.mp3");
        File.Copy(Mp3, locked, true);
        using (var exclusive = new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert(!MusicStore.CanRead(locked), "Locked file was readable");
        Assert(MusicStore.CanRead(locked), "Accessible source was rejected");
        var data = new MusicLibraryData { CurrentTrackPath = missing, Playlists = [
            new MusicPlaylist { Name = "A", Tracks = [new MusicTrack { FilePath = missing }, new MusicTrack { FilePath = Mp3 }] },
            new MusicPlaylist { Name = "B", Tracks = [new MusicTrack { FilePath = missing }] }] };
        Assert(!MusicStore.CanRead(missing), "Missing file accepted");
        Assert(MusicStore.Prune(data, [missing]) == 2 && data.CurrentTrackPath is null, "Prune did not cover all playlists");
        Assert(data.Playlists[0].Tracks.Single().FilePath == Mp3 && File.Exists(Mp3), "Prune affected accessible files");
        File.Delete(locked);
    }

    private static void CorruptStorage()
    {
        string path = Path.Combine(Output, "corrupt.json");
        File.WriteAllText(path, "{ broken json");
        _ = MusicStore.Load(path, out string error);
        Assert(error != "", "Corruption was silent");
        var view = new MusicPlayerView(path);
        view.ImportFiles([Mp3]);
        view.Dispose();
        Assert(File.ReadAllText(path) == "{ broken json", "Corrupt file was overwritten");
    }

    private static float[] Analyse(double frequency, double amplitude)
    {
        var provider = new SpectrumSampleProvider(new Tone(frequency, amplitude));
        var buffer = new float[8192];
        provider.Read(buffer, 0, buffer.Length);
        var result = new float[SpectrumSampleProvider.BandCount];
        provider.CopyLevels(result);
        return result;
    }

    private static void Spectrum()
    {
        float[] bass = Analyse(110, 0.8), treble = Analyse(6000, 0.8), quiet = Analyse(110, 0.02), silence = Analyse(110, 0);
        int bassBand = Array.IndexOf(bass, bass.Max()), trebleBand = Array.IndexOf(treble, treble.Max());
        Assert(bassBand < 4 && trebleBand > 8, "Frequency did not move to the expected bands");
        Assert(bass.Max() > quiet.Max() && silence.All(v => v == 0), "Amplitude/silence response failed");
        Assert(bass.Concat(treble).All(v => v is >= 0 and <= 1 && float.IsFinite(v)), "Invalid spectrum level");
    }

    private static void Playback()
    {
        var fake = new FakeOutput();
        using (var player = new MusicPlayback(() => fake))
        {
            player.Play(Mp3);
            Assert(player.IsPlaying && fake.Source is not null, "Play did not initialise output");
            var buffer = new byte[65536];
            Assert(fake.Source!.Read(buffer, 0, buffer.Length) > 0, "Real MP3 did not decode");
            player.Pause();
            var levels = new float[12];
            player.CopyLevels(levels);
            Assert(!player.IsPlaying && levels.All(v => v == 0), "Pause did not flatten spectrum");
            player.Play(Mp3);
            Assert(player.IsPlaying && fake.Initialisations == 1, "Resume reopened the source");
            bool ended = false;
            player.Stopped += _ => ended = true;
            fake.Complete();
            Assert(ended, "Completion notification absent");
        }
        using var unlocked = new FileStream(Mp3, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert(fake.Disposed, "Output was not disposed");
    }

    private static void Interaction()
    {
        string path = Path.Combine(Output, "interaction.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new MusicLibraryData { Playlists = [new MusicPlaylist { Name = "Nhạc của tôi" }] }));
        var outputs = new List<FakeOutput>();
        var player = new MusicPlayback(() => { var item = new FakeOutput(); outputs.Add(item); return item; });
        var view = new MusicPlayerView(path, player);
        var window = Host(view);
        window.Show();
        Pump(150);
        view.ImportFiles([Mp3, Mp3.ToUpperInvariant(), Path.Combine(Output, "missing.mp3")]);
        var list = (ListBox)view.FindName("TrackList");
        Assert(list.Items.Count == 1, "Import duplicated/rejected valid source");
        Click(view, "PlayButton");
        Assert(player.IsPlaying, "Play button failed");
        Pump(120);
        Assert(((System.Windows.Media.Animation.Animatable)view.FindName("TitleOffset")).HasAnimatedProperties, "Marquee is not running");
        Click(view, "PlayButton");
        Assert(!player.IsPlaying, "Pause button failed");
        Click(view, "PlayButton");
        outputs[^1].Complete();
        Pump(100);
        Assert(player.IsPlaying && outputs.Count == 2 && outputs[0].Disposed, "Playlist did not loop/release previous output");
        view.ImportFiles([Mp3]);
        Assert(player.IsPlaying, "Browsing/importing interrupted playback");
        view.Dispose();
        window.Close();
        var data = MusicStore.Load(path, out _);
        Assert(data.Playlists.Count == 1 && data.Playlists[0].Tracks.Count == 1, "Music list changes were not persisted");
    }

    private static Window Host(MusicPlayerView view)
    {
        var grid = new Grid { Width = 220, Height = 400, Background = Brushes.Transparent };
        grid.Resources.MergedDictionaries.Add(new ThemeResources { WallpaperAware = true });
        view.Margin = new Thickness(20, 100, 20, 0);
        view.VerticalAlignment = VerticalAlignment.Top;
        grid.Children.Add(view);
        return new Window { Content = grid, Width = 240, Height = 450, Left = 20, Top = 20, ShowInTaskbar = false };
    }

    private static (MusicPlayerView View, Window Window, MusicPlaylist Playlist, List<FakeOutput> Outputs, MusicPlayback Player) MusicFixture(string name, int count = 3)
    {
        string path = Path.Combine(Output, name + ".json");
        var playlist = new MusicPlaylist { Name = "Nhạc tập trung" };
        for (int i = 0; i < count; i++)
        {
            string trackPath = i == 0 ? Mp3 : Path.Combine(Output, $"Bài nhạc {i + 1}.mp3");
            if (i != 0) File.Copy(Mp3, trackPath, true);
            playlist.Tracks.Add(new MusicTrack { FilePath = trackPath });
        }
        MusicStore.Save(new MusicLibraryData { Playlists = [playlist], SelectedPlaylistId = playlist.Id }, path, out _);
        var outputs = new List<FakeOutput>();
        var player = new MusicPlayback(() => { var output = new FakeOutput(); outputs.Add(output); return output; });
        var view = new MusicPlayerView(path, player);
        var window = Host(view);
        window.Show();
        Click(view, "LibraryButton");
        Pump(160);
        var active = (MusicPlaylist)typeof(MusicPlayerView).GetProperty("SelectedPlaylist", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view)!;
        return (view, window, active, outputs, player);
    }

    private static void PlayModes()
    {
        var f = MusicFixture("modes");
        try
        {
            Assert(MusicSequence.Next(f.Playlist, f.Playlist.Tracks[2], MusicPlayMode.RepeatAll) == f.Playlist.Tracks[0], "Repeat-all did not wrap");
            var choices = new HashSet<MusicTrack>();
            var random = new Random(17);
            for (int i = 0; i < 60; i++)
            {
                var choice = MusicSequence.Next(f.Playlist, f.Playlist.Tracks[0], MusicPlayMode.Shuffle, random)!;
                Assert(choice != f.Playlist.Tracks[0], "Shuffle immediately repeated a track");
                choices.Add(choice);
            }
            Assert(choices.Count == 2 && MusicSequence.Next(new MusicPlaylist(), null, MusicPlayMode.Shuffle) is null, "Shuffle edge cases failed");
            f.View.PlayTrack(f.Playlist.Tracks[0], f.Playlist);
            Click(f.View, "RepeatOneButton");
            f.Outputs[^1].Complete();
            Pump(90);
            Assert(f.Player.FilePath == f.Playlist.Tracks[0].FilePath && f.Player.IsPlaying, "Repeat-one did not restart the same track");
            Click(f.View, "RepeatAllButton");
            f.Outputs[^1].Complete();
            Pump(90);
            Assert(f.Player.FilePath == f.Playlist.Tracks[1].FilePath, "Repeat-all did not advance");
            Click(f.View, "ShuffleButton");
            string? previous = f.Player.FilePath;
            f.Outputs[^1].Complete();
            Pump(90);
            Assert(f.Player.FilePath != previous && f.Player.IsPlaying, "Shuffle did not change tracks");
            Assert(f.Playlist.Tracks.Count(t => t.IsPlaying) == 1, "Playing state was not exclusive");
            f.View.Dispose();
            Assert(MusicStore.Load(Path.Combine(Output, "modes.json"), out _).PlayMode == MusicPlayMode.Shuffle, "Mode did not persist");
        }
        finally { f.View.Dispose(); f.Window.Close(); }
    }

    private static void Reordering()
    {
        var f = MusicFixture("reorder");
        try
        {
            var list = (ListBox)f.View.FindName("TrackList");
            var first = f.Playlist.Tracks[0];
            f.View.PlayTrack(first, f.Playlist);
            var firstRow = (FrameworkElement)list.ItemContainerGenerator.ContainerFromIndex(0);
            var lastRow = (FrameworkElement)list.ItemContainerGenerator.ContainerFromIndex(2);
            var start = firstRow.TranslatePoint(new Point(8, 10), list);
            var end = lastRow.TranslatePoint(new Point(8, lastRow.ActualHeight - 10), list);
            Assert(f.View.BeginReorder(list, first, start), "Could not begin drag");
            f.View.UpdateReorder(end);
            Assert(f.View.ReorderDestination == 2 && f.Playlist.Tracks[0] == first, "Preview mutated order or picked wrong destination");
            if (SystemParameters.ClientAreaAnimation)
                Assert(lastRow.RenderTransform.HasAnimatedProperties, "Rows did not animate out of the way");
            f.View.FinishReorder(false);
            Assert(f.Playlist.Tracks[0] == first && !f.View.IsReordering, "Cancel changed order");
            Pump(220);
            Assert(f.View.BeginReorder(list, first, start), "Second drag failed");
            f.View.UpdateReorder(end);
            f.View.FinishReorder(true);
            Assert(f.Playlist.Tracks[2] == first && f.Player.IsPlaying && f.Player.FilePath == first.FilePath, "Commit changed playback or lost track identity");
            Pump(220);
            Assert(f.View.MoveItem(list, first, 0), "Keyboard reorder failed");
            Assert(f.Playlist.Tracks[0] == first, "Keyboard reorder selected wrong destination");
            Assert(!f.View.MoveItem(list, first, -1), "Invalid destination accepted");
            f.View.Dispose();
            var saved = MusicStore.Load(Path.Combine(Output, "reorder.json"), out _);
            Assert(saved.Playlists[0].Id == f.Playlist.Id && saved.Playlists[0].Tracks[0].FilePath == first.FilePath, "Reordered entries did not persist");
        }
        finally { f.View.Dispose(); f.Window.Close(); }
    }

    private static List<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        var result = new List<T>();
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) result.Add(match);
            result.AddRange(Descendants<T>(child));
        }
        return result;
    }

    private static bool HasSignal(SpectrumView spectrum) =>
        ((float[])typeof(SpectrumView).GetField("heights", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(spectrum)!).Any(v => v > 0.001);

    private static void RowSpectrum()
    {
        var f = MusicFixture("row-spectrum");
        try
        {
            var list = (ListBox)f.View.FindName("TrackList");
            var spectra = Descendants<SpectrumView>(list);
            Assert(spectra.Count == 3 && spectra.All(s => !HasSignal(s)), "Every row must start with a flat spectrum");
            var buffer = new byte[65536];
            f.View.PlayTrack(f.Playlist.Tracks[0], f.Playlist);
            ReadUntilSignal(f.Player, f.Outputs[^1]);
            Pump(120);
            if (SystemParameters.ClientAreaAnimation)
                Assert(spectra.Count(HasSignal) == 1 && HasSignal(spectra.First(s => ReferenceEquals(s.DataContext, f.Playlist.Tracks[0]))), $"Spectrum did not follow the first track exclusively ({spectra.Count(HasSignal)} active rows)");
            f.View.PlayTrack(f.Playlist.Tracks[1], f.Playlist);
            ReadUntilSignal(f.Player, f.Outputs[^1]);
            Pump(120);
            if (SystemParameters.ClientAreaAnimation)
                Assert(spectra.Count(HasSignal) == 1 && HasSignal(spectra.First(s => ReferenceEquals(s.DataContext, f.Playlist.Tracks[1]))), "Spectrum did not transfer to the new track");
            SaveImage((FrameworkElement)((Popup)f.View.FindName("LibraryPopup")).Child, "playlist-playing.png");
            Click(f.View, "PlayButton");
            Assert(spectra.All(s => !HasSignal(s)) && f.Playlist.Tracks.All(t => !t.IsPlaying), "Pause left a row animated");
        }
        finally { f.View.Dispose(); f.Window.Close(); }
    }

    private static void ReadUntilSignal(MusicPlayback player, FakeOutput output)
    {
        var buffer = new byte[8192];
        var levels = new float[12];
        for (int i = 0; i < 500; i++)
        {
            if (output.Source!.Read(buffer, 0, buffer.Length) == 0) break;
            player.CopyLevels(levels);
            if (levels.Any(v => v > 0.01)) return;
        }
        throw new InvalidOperationException("The MP3 fixture produced no audible PCM signal");
    }

    private static void AlignmentAndMarquee()
    {
        var f = MusicFixture("alignment", 1);
        try
        {
            Pump(90);
            foreach (string name in new[] { "LibraryButton", "AddMusicButton" })
            {
                var button = (Button)f.View.FindName(name);
                var icon = (FrameworkElement)button.Content;
                Point center = icon.TranslatePoint(new Point(icon.ActualWidth / 2, icon.ActualHeight / 2), button);
                Assert(Math.Abs(center.X - button.ActualWidth / 2) < 1 && Math.Abs(center.Y - button.ActualHeight / 2) < 1, name + " is not centred");
            }
            if (SystemParameters.ClientAreaAnimation)
            {
                Pump(1350);
                var offset = (TranslateTransform)f.View.FindName("TitleOffset");
                double first = offset.X;
                Pump(150);
                Assert(offset.X > first, "Title moved in the wrong direction");
            }
        }
        finally { f.View.Dispose(); f.Window.Close(); }
    }

    private static void Render()
    {
        foreach (bool dark in new[] { false, true })
        {
            ThemeManager.Apply(dark ? AppTheme.Dark : AppTheme.Light);
            string renderPath = Path.Combine(Output, $"render-{dark}.json");
            File.Delete(renderPath);
            var view = new MusicPlayerView(renderPath);
            var window = Host(view);
            var root = (Grid)window.Content;
            root.SetResourceReference(Panel.BackgroundProperty, "CanvasBrush");
            window.Show();
            view.ImportFiles([Mp3]);
            foreach (string name in new[] { "Lofi · Góc học tập.mp3", "Piano · Buổi tối yên tĩnh.mp3" })
            {
                string song = Path.Combine(Output, name);
                File.Copy(Mp3, song, true);
                view.ImportFiles([song]);
            }
            Pump(100);
            SaveImage(root, $"player-{dark}.png");
            Click(view, "LibraryButton");
            Pump(100);
            var popup = (Popup)view.FindName("LibraryPopup");
            Assert(popup.IsOpen && popup.Child is FrameworkElement, "Playlist popup did not open");
            SaveImage((FrameworkElement)popup.Child, $"playlist-{dark}.png");
            popup.IsOpen = false;
            var image = new Image
            {
                Source = new BitmapImage(new Uri("pack://application:,,,/Pomodoro;component/Assets/Images/studyTime.jpg")),
                Stretch = Stretch.UniformToFill
            };
            root.Children.Insert(0, image);
            view.WallpaperSource = image;
            ThemeManager.SetBackground(true, 0.35);
            Pump(100);
            SaveImage(root, $"blur-{dark}.png");
            Assert(view.ActualWidth == 180 && view.ActualHeight < 100, "Player exceeded rail dimensions");
            view.Dispose();
            window.Close();
            ThemeManager.SetBackground(false, 0.9);
        }
        // Exercise the real shell at its minimum supported size without importing
        // test tracks into the user's music library.
        var shell = new Pomodoro.Shell.MainWindow { WindowState = WindowState.Normal, Width = 820, Height = 700 };
        shell.Show();
        Pump(150);
        SaveImage((FrameworkElement)shell.Content, "shell-minimum.png");
        var music = (MusicPlayerView)shell.FindName("MusicPlayer");
        var footer = (Button)shell.FindName("TodoToggleButton");
        double bottom = music.TranslatePoint(new Point(0, music.ActualHeight), shell).Y;
        Assert(bottom < footer.TranslatePoint(new Point(), shell).Y, "Music overlaps sidebar footer");
        shell.Close();
    }

    private static void Click(MusicPlayerView view, string name) => ((Button)view.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static void SaveImage(FrameworkElement element, string name)
    {
        element.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(Output, name));
        encoder.Save(file);
    }

    private static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private sealed class Tone(double frequency, double amplitude) : ISampleProvider
    {
        private int sample;
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        public int Read(float[] buffer, int offset, int count)
        {
            for (int i = 0; i < count; i += 2)
            {
                float value = (float)(amplitude * Math.Sin(2 * Math.PI * frequency * sample++ / 44100));
                buffer[offset + i] = buffer[offset + i + 1] = value;
            }
            return count;
        }
    }

    private sealed class FakeOutput : IWavePlayer
    {
        public IWaveProvider? Source;
        public int Initialisations;
        public bool Disposed;
        public PlaybackState PlaybackState { get; private set; }
        public float Volume { get; set; }
        public WaveFormat OutputWaveFormat => Source!.WaveFormat;
        public event EventHandler<StoppedEventArgs>? PlaybackStopped;
        public void Init(IWaveProvider waveProvider) { Source = waveProvider; Initialisations++; }
        public void Play() => PlaybackState = PlaybackState.Playing;
        public void Pause() => PlaybackState = PlaybackState.Paused;
        public void Stop() => PlaybackState = PlaybackState.Stopped;
        public void Complete() { Stop(); PlaybackStopped?.Invoke(this, new StoppedEventArgs()); }
        public void Dispose() { Stop(); Disposed = true; }
    }
}
