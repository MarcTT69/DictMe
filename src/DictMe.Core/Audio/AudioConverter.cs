namespace DictMe.Core.Audio;

/// <summary>
/// Prépare l'audio pour Whisper, qui attend du mono 16 kHz en float.
/// </summary>
public static class AudioConverter
{
    public const int WhisperSampleRate = 16_000;

    public static float[] ToWhisperInput(WavAudio audio)
    {
        ArgumentNullException.ThrowIfNull(audio);
        return Resample(ToMono(audio), audio.SampleRate, WhisperSampleRate);
    }

    public static float[] ToMono(WavAudio audio)
    {
        if (audio.Channels == 1)
            return audio.Samples;

        int frames = audio.FrameCount;
        var mono = new float[frames];
        for (int f = 0; f < frames; f++)
        {
            float sum = 0;
            int offset = f * audio.Channels;
            for (int c = 0; c < audio.Channels; c++)
                sum += audio.Samples[offset + c];
            mono[f] = sum / audio.Channels;
        }
        return mono;
    }

    /// <summary>
    /// Rééchantillonne un signal mono. En sous-échantillonnage, chaque échantillon de sortie est la moyenne
    /// des échantillons d'entrée qu'il couvre (filtre passe-bas simple, suffisant pour la voix) ;
    /// en sur-échantillonnage, on interpole linéairement.
    /// </summary>
    public static float[] Resample(float[] input, int sourceRate, int targetRate)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetRate);

        if (sourceRate == targetRate || input.Length == 0)
            return input;

        double ratio = (double)sourceRate / targetRate;
        int outputLength = (int)Math.Floor(input.Length / ratio);
        var output = new float[outputLength];

        if (ratio > 1)
        {
            for (int i = 0; i < outputLength; i++)
            {
                int start = (int)Math.Floor(i * ratio);
                int end = Math.Min(input.Length, Math.Max(start + 1, (int)Math.Floor((i + 1) * ratio)));
                float sum = 0;
                for (int j = start; j < end; j++)
                    sum += input[j];
                output[i] = sum / (end - start);
            }
        }
        else
        {
            for (int i = 0; i < outputLength; i++)
            {
                double position = i * ratio;
                int index = (int)position;
                double fraction = position - index;
                float a = input[index];
                float b = index + 1 < input.Length ? input[index + 1] : a;
                output[i] = (float)(a + (b - a) * fraction);
            }
        }

        return output;
    }
}
