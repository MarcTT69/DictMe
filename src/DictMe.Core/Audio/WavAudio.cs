namespace DictMe.Core.Audio;

/// <summary>
/// Audio PCM décodé. Les échantillons sont entrelacés (L, R, L, R...) et normalisés entre -1 et 1.
/// </summary>
public sealed record WavAudio(int SampleRate, int Channels, float[] Samples)
{
    public int FrameCount => Channels == 0 ? 0 : Samples.Length / Channels;

    public TimeSpan Duration => SampleRate == 0
        ? TimeSpan.Zero
        : TimeSpan.FromSeconds((double)FrameCount / SampleRate);
}
