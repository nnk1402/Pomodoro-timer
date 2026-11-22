using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Pomodoro;

// Owned by MainWindow, never by a timer page. Local URI playback uses the native
// media pipeline's buffering; no video bytes are read into managed memory.
internal sealed class BackgroundController : IDisposable
{
    private readonly Window window;
    private readonly Image image;
    private readonly System.Windows.Shapes.Rectangle shade;
    private readonly MediaPlayer player = new() { IsMuted = true, Volume = 0, ScrubbingEnabled = true };
    private readonly DispatcherTimer openingTimeout;
    private bool disposed;
    private bool opened;
    private readonly Func<bool> reduceMotion;
    private FileSystemWatcher? sourceWatcher;
    private long sourceVersion;

    internal BackgroundOptions Current { get; private set; } = new();
    internal bool IsLoading { get; private set; }
    internal bool IsPlaying { get; private set; }
    internal string? Error { get; private set; }
    internal MediaPlayer Player => player;
    internal event Action? Changed;

    internal BackgroundController(Window window, Image image, System.Windows.Shapes.Rectangle shade,
        Func<bool>? reduceMotion = null)
    {
        this.window = window;
        this.image = image;
        this.shade = shade;
        this.reduceMotion = reduceMotion ?? (() => !SystemParameters.ClientAreaAnimation || SystemParameters.HighContrast);
        openingTimeout = new DispatcherTimer(TimeSpan.FromSeconds(15), DispatcherPriority.Background,
            (_, _) => Fail("Video không phản hồi khi mở. Hãy thử tệp MP4 khác hoặc kiểm tra codec của Windows."), window.Dispatcher);
        openingTimeout.Stop();
        player.MediaOpened += MediaOpened;
        player.MediaFailed += MediaFailed;
        player.MediaEnded += MediaEnded;
        window.StateChanged += WindowStateChanged;
        SystemParameters.StaticPropertyChanged += SystemPropertyChanged;
    }

    internal void Apply(BackgroundOptions options)
    {
        if (disposed) return;
        if (!options.IsValid) { Fail("Thông số hình nền không hợp lệ. Đã dùng nền mặc định."); return; }
        if (Current.Kind == options.Kind && string.Equals(Current.Path, options.Path, StringComparison.OrdinalIgnoreCase)
            && (opened || IsLoading || options.Kind == BackgroundKind.Default))
        {
            if (options.Kind != BackgroundKind.Default && !File.Exists(options.Path))
            { Fail("Không còn tìm thấy tệp hình nền. Hãy chọn lại tệp."); return; }
            Current = options;
            Error = null;
            UpdateSurfaces();
            Changed?.Invoke();
            return;
        }

        ReleaseSource();
        Current = options;
        Error = null;
        try
        {
            if (options.Kind == BackgroundKind.Default) { UpdateSurfaces(); Changed?.Invoke(); return; }
            if (string.IsNullOrWhiteSpace(options.Path) || !System.IO.Path.IsPathFullyQualified(options.Path)
                || !File.Exists(options.Path))
            { Fail("Không tìm thấy tệp hình nền. Tệp có thể đã bị xóa hoặc di chuyển; hãy chọn lại tệp."); return; }
            WatchSource(options.Path);
            if (options.Kind == BackgroundKind.Image)
            {
                string extension = System.IO.Path.GetExtension(options.Path).ToLowerInvariant();
                if (extension is not (".jpg" or ".jpeg" or ".png"))
                { Fail("Ảnh nền cần là tệp JPG, JPEG hoặc PNG."); return; }
                // Decode once and close the stream immediately, including on failure.
                using var stream = File.OpenRead(options.Path);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = Math.Clamp((int)(SystemParameters.PrimaryScreenWidth * 1.5), 1920, 3840);
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                image.Source = bitmap;
                opened = true;
            }
            else
            {
                IsLoading = true;
                openingTimeout.Start();
                player.Open(new Uri(options.Path, UriKind.Absolute));
                // MediaOpened/MediaFailed, rather than the file extension, decides support.
            }
            UpdateSurfaces();
            Changed?.Invoke();
        }
        catch (Exception error) when (IsMediaError(error))
        { Fail("Không thể mở hình nền. Tệp bị hỏng, không được hỗ trợ hoặc không có quyền đọc. Hãy chọn tệp khác."); }
    }

    private static bool IsMediaError(Exception e) => e is IOException or UnauthorizedAccessException
        or NotSupportedException or ArgumentException or InvalidOperationException
        or System.Runtime.InteropServices.COMException or System.Security.SecurityException or FileFormatException;

    private void WatchSource(string path)
    {
        long version = sourceVersion;
        sourceWatcher = new FileSystemWatcher(System.IO.Path.GetDirectoryName(path)!, System.IO.Path.GetFileName(path))
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
        };
        void SourceChanged()
        {
            if (window.Dispatcher.HasShutdownStarted) return;
            window.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!disposed && version == sourceVersion)
                    Fail("Tệp hình nền đã bị thay đổi, xóa hoặc di chuyển. Đã dùng nền mặc định; hãy chọn lại tệp.");
            }));
        }
        sourceWatcher.Deleted += (_, _) => SourceChanged();
        sourceWatcher.Renamed += (_, _) => SourceChanged();
        sourceWatcher.Changed += (_, _) => SourceChanged();
        sourceWatcher.Error += (_, _) => SourceChanged();
        sourceWatcher.EnableRaisingEvents = true;
    }

    private void MediaOpened(object? sender, EventArgs e)
    {
        if (disposed || Current.Kind != BackgroundKind.Video || !IsLoading) return;
        openingTimeout.Stop();
        if (!player.HasVideo || player.NaturalVideoWidth <= 0 || player.NaturalVideoHeight <= 0)
        { Fail("Tệp không có hình ảnh video phát được. Hãy chọn video khác; MP4 với H.264 thường phù hợp."); return; }
        try
        {
            image.Source = new DrawingImage(new VideoDrawing
            {
                Player = player,
                Rect = new Rect(0, 0, player.NaturalVideoWidth, player.NaturalVideoHeight)
            });
            IsLoading = false;
            opened = true;
            UpdatePlayback();
            UpdateSurfaces();
            Changed?.Invoke();
        }
        catch (Exception error) when (IsMediaError(error)) { Fail("Windows không thể phát video này. Hãy thử video hoặc codec khác."); }
    }

    private void MediaFailed(object? sender, ExceptionEventArgs e)
    {
        if (!disposed && Current.Kind == BackgroundKind.Video)
            Fail("Không thể phát video. Tệp có thể bị hỏng hoặc Windows thiếu codec phù hợp. Hãy thử MP4 với H.264.");
    }

    private void MediaEnded(object? sender, EventArgs e)
    {
        if (disposed || !opened || Current.Kind != BackgroundKind.Video) return;
        try { player.Position = TimeSpan.Zero; UpdatePlayback(); }
        catch (Exception error) when (IsMediaError(error)) { Fail("Không thể phát lại video. Hãy chọn lại tệp hình nền."); }
    }

    private void WindowStateChanged(object? sender, EventArgs e) => UpdatePlayback();
    private void SystemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (disposed || window.Dispatcher.HasShutdownStarted) return;
        window.Dispatcher.BeginInvoke(new Action(() => { if (!disposed) UpdatePlayback(); }));
    }

    internal void UpdatePlayback()
    {
        if (disposed || !opened || Current.Kind != BackgroundKind.Video) return;
        if (!File.Exists(Current.Path)) { Fail("Tệp video không còn ở vị trí đã chọn. Đã dùng nền mặc định."); return; }
        try
        {
            player.IsMuted = true;
            player.Volume = 0;
            IsPlaying = window.WindowState != WindowState.Minimized && !reduceMotion();
            if (IsPlaying) player.Play();
            else player.Pause(); // ScrubbingEnabled keeps a still frame for reduced motion.
        }
        catch (Exception error) when (IsMediaError(error)) { Fail("Không thể tiếp tục video. Hãy chọn lại tệp hình nền."); }
    }

    private void UpdateSurfaces()
    {
        bool active = opened && Current.Kind != BackgroundKind.Default;
        shade.Opacity = active ? Current.Darkness : 0;
        ThemeManager.SetBackground(active, Current.SurfaceOpacity);
    }

    private void Fail(string message)
    {
        ReleaseSource();
        Current = new BackgroundOptions();
        Error = message;
        UpdateSurfaces();
        Changed?.Invoke();
    }

    private void ReleaseSource()
    {
        sourceVersion++;
        sourceWatcher?.Dispose();
        sourceWatcher = null;
        openingTimeout.Stop();
        IsLoading = opened = IsPlaying = false;
        image.Source = null;
        // Close releases the native decoder and its file handle, unlike Stop.
        player.Close();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        window.StateChanged -= WindowStateChanged;
        SystemParameters.StaticPropertyChanged -= SystemPropertyChanged;
        player.MediaOpened -= MediaOpened;
        player.MediaFailed -= MediaFailed;
        player.MediaEnded -= MediaEnded;
        ReleaseSource();
        Current = new();
        UpdateSurfaces();
        Changed = null;
    }
}
