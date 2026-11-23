using NAudio.Dsp;
using NAudio.Wave;

namespace Pomodoro.Features.Music;

// Analyse the same PCM samples sent to the output, never desktop/microphone audio.
internal sealed class SpectrumSampleProvider(ISampleProvider source) : ISampleProvider
{
    internal const int BandCount = 12;
    private const int FftSize = 2048;
    private readonly Complex[] fft = new Complex[FftSize];
    private readonly float[] levels = new float[BandCount];
    private readonly object gate = new();
    private int position;
    private int channel;
    private float mono;
    public WaveFormat WaveFormat => source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        int read = source.Read(buffer, offset, count);
        for (int i = offset; i < offset + read; i++)
        {
            mono += buffer[i];
            if (++channel < WaveFormat.Channels) continue;
            fft[position].X = mono / channel * (float)FastFourierTransform.HannWindow(position, FftSize);
            fft[position].Y = 0;
            channel = 0;
            mono = 0;
            if (++position != FftSize) continue;
            position = 0;
            FastFourierTransform.FFT(true, 11, fft);
            lock (gate)
            {
                double maxFrequency = Math.Min(16000, WaveFormat.SampleRate / 2d);
                for (int band = 0; band < BandCount; band++)
                {
                    double low = 50 * Math.Pow(maxFrequency / 50, band / (double)BandCount);
                    double high = 50 * Math.Pow(maxFrequency / 50, (band + 1d) / BandCount);
                    int first = Math.Clamp((int)(low * FftSize / WaveFormat.SampleRate), 1, FftSize / 2 - 1);
                    int last = Math.Clamp((int)Math.Ceiling(high * FftSize / WaveFormat.SampleRate), first + 1, FftSize / 2);
                    double peak = 0;
                    for (int bin = first; bin < last; bin++)
                        peak = Math.Max(peak, Math.Sqrt(fft[bin].X * fft[bin].X + fft[bin].Y * fft[bin].Y));
                    // Fixed dB scale: silence stays flat, louder music grows taller.
                    levels[band] = (float)Math.Clamp((20 * Math.Log10(Math.Max(peak, 1e-7)) + 65) / 55, 0, 1);
                }
            }
        }
        return read;
    }

    internal void CopyLevels(float[] destination)
    {
        lock (gate) Array.Copy(levels, destination, Math.Min(destination.Length, levels.Length));
    }
}
