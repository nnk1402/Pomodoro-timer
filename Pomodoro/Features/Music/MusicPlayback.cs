using NAudio.Wave;

namespace Pomodoro.Features.Music;

internal sealed class MusicPlayback : IDisposable
{
    private readonly Func<IWavePlayer> createOutput;
    private IWavePlayer? output;
    private AudioFileReader? reader;
    private SpectrumSampleProvider? spectrum;
    internal event Action<Exception?>? Stopped;
    internal bool IsPlaying => output?.PlaybackState == PlaybackState.Playing;
    internal string? FilePath { get; private set; }
    internal int Version { get; private set; }

    internal MusicPlayback(Func<IWavePlayer>? createOutput = null) =>
        this.createOutput = createOutput ?? (() => new WaveOutEvent { DesiredLatency = 80, NumberOfBuffers = 3 });

    internal void Play(string path)
    {
        if (reader is not null && string.Equals(path, FilePath, StringComparison.OrdinalIgnoreCase))
        {
            output!.Play();
            return;
        }
        Stop();
        try
        {
            reader = new AudioFileReader(path);
            spectrum = new SpectrumSampleProvider(reader);
            output = createOutput();
            output.Init(spectrum.ToWaveProvider());
            output.PlaybackStopped += OutputStopped;
            FilePath = path;
            output.Play();
        }
        catch
        {
            Stop();
            throw;
        }
    }

    private void OutputStopped(object? sender, StoppedEventArgs e)
    {
        if (ReferenceEquals(sender, output)) Stopped?.Invoke(e.Exception);
    }

    internal void Pause() => output?.Pause();

    internal void CopyLevels(float[] destination)
    {
        if (IsPlaying && spectrum is not null) spectrum.CopyLevels(destination);
        else Array.Clear(destination);
    }

    internal void Stop()
    {
        Version++;
        if (output is { } old)
        {
            output = null;
            old.PlaybackStopped -= OutputStopped;
            old.Stop();
            old.Dispose();
        }
        reader?.Dispose();
        reader = null;
        spectrum = null;
        FilePath = null;
    }

    public void Dispose() => Stop();
}
