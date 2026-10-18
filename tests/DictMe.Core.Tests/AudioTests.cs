using DictMe.Core.Audio;

namespace DictMe.Core.Tests;

public class AudioTests
{
    [Fact]
    public void Reads_pcm16_mono()
    {
        var wav = WavBuilder.Pcm16([0, 16384, -32768, 32767], 16000, 1);

        var audio = WavReader.Read(wav);

        Assert.Equal(16000, audio.SampleRate);
        Assert.Equal(1, audio.Channels);
        Assert.Equal([0f, 0.5f, -1f, 32767f / 32768f], audio.Samples);
    }

    [Fact]
    public void Tolerates_zero_data_size_and_extra_chunks()
    {
        var wav = WavBuilder.Pcm16([100, 200, 300, 400], 44100, 2, zeroDataSize: true, withListChunk: true);

        var audio = WavReader.Read(new MemoryStream(wav));

        Assert.Equal(2, audio.Channels);
        Assert.Equal(4, audio.Samples.Length);
        Assert.Equal(2, audio.FrameCount);
    }

    [Fact]
    public void Reads_float32()
    {
        var audio = WavReader.Read(WavBuilder.Float32([0.25f, -0.75f], 48000));

        Assert.Equal([0.25f, -0.75f], audio.Samples);
    }

    [Fact]
    public void Rejects_non_wav_data()
    {
        Assert.Throws<InvalidDataException>(() => WavReader.Read("not a wav file at all"u8));
    }

    [Fact]
    public void Downmixes_stereo_to_mono()
    {
        var audio = new WavAudio(16000, 2, [1f, 0f, 0.5f, 0.5f]);

        Assert.Equal([0.5f, 0.5f], AudioConverter.ToMono(audio));
    }

    [Fact]
    public void Resamples_44100_to_16000_preserving_duration_and_level()
    {
        var input = Enumerable.Repeat(0.5f, 44100).ToArray();

        var output = AudioConverter.Resample(input, 44100, 16000);

        Assert.Equal(16000, output.Length);
        Assert.All(output, s => Assert.Equal(0.5f, s, 5));
    }

    [Fact]
    public void Upsamples_by_linear_interpolation()
    {
        var output = AudioConverter.Resample([0f, 1f], 8000, 16000);

        Assert.Equal([0f, 0.5f, 1f, 1f], output);
    }

    [Fact]
    public void Converts_recorder_output_to_whisper_input()
    {
        var samples = Enumerable.Range(0, 44100 * 2).Select(i => (short)(i % 2 == 0 ? 8192 : -8192)).ToArray();
        var audio = WavReader.Read(WavBuilder.Pcm16(samples, 44100, 2));

        var whisper = AudioConverter.ToWhisperInput(audio);

        Assert.Equal(16000, whisper.Length);
        Assert.All(whisper, s => Assert.Equal(0f, s, 5));
        Assert.Equal(TimeSpan.FromSeconds(1), audio.Duration);
    }
}
