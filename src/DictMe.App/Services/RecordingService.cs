using Plugin.Maui.Audio;

namespace DictMe.App.Services;

/// <summary>Enregistrement du micro dans un fichier WAV temporaire.</summary>
public sealed class RecordingService(IAudioManager audioManager)
{
    // 44,1 kHz est la seule fréquence garantie sur tous les Android ; l'audio est ensuite converti en 16 kHz pour Whisper.
    private static readonly AudioRecorderOptions Options = new()
    {
        SampleRate = 44100,
        Channels = ChannelType.Mono,
        BitDepth = BitDepth.Pcm16bit,
        Encoding = Plugin.Maui.Audio.Encoding.Wav,
        ThrowIfNotSupported = false,
    };

    private IAudioRecorder? _recorder;

    private static string RecordingPath => Path.Combine(FileSystem.CacheDirectory, "dictme-recording.wav");

    public bool IsRecording => _recorder?.IsRecording == true;

    public async Task<bool> EnsurePermissionAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Microphone>();
        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<Permissions.Microphone>();
        return status == PermissionStatus.Granted;
    }

    public async Task StartAsync()
    {
        if (IsRecording)
            return;

        if (File.Exists(RecordingPath))
            File.Delete(RecordingPath);

        _recorder = audioManager.CreateRecorder();
        await _recorder.StartAsync(RecordingPath, Options);
    }

    /// <summary>Arrête l'enregistrement et renvoie le WAV en mémoire.</summary>
    public async Task<Stream> StopAsync()
    {
        if (_recorder is null)
            throw new InvalidOperationException("Aucun enregistrement en cours.");

        var source = await _recorder.StopAsync();
        _recorder = null;

        var memory = new MemoryStream();
        await using (var audio = source.GetAudioStream())
        {
            await audio.CopyToAsync(memory);
        }
        memory.Position = 0;

        try
        {
            File.Delete(RecordingPath);
        }
        catch (IOException)
        {
            // Sans importance : le fichier est écrasé au prochain enregistrement.
        }

        return memory;
    }
}
