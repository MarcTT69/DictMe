using DictMe.Core.Audio;
using DictMe.Core.Text;

namespace DictMe.Core.Transcription;

public sealed record DictationResult(string Text, TimeSpan AudioDuration);

/// <summary>
/// Enregistrement WAV → audio 16 kHz mono → Whisper → texte mis en forme.
/// </summary>
public sealed class DictationPipeline(ITranscriber transcriber)
{
    private static readonly TimeSpan MinimumDuration = TimeSpan.FromMilliseconds(300);

    public async Task<DictationResult> ProcessAsync(Stream wav, string modelPath, DictationSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(wav);
        ArgumentNullException.ThrowIfNull(settings);

        var audio = WavReader.Read(wav);
        if (audio.Duration < MinimumDuration)
            return new DictationResult(string.Empty, audio.Duration);

        var samples = AudioConverter.ToWhisperInput(audio);
        var request = new TranscriptionRequest(modelPath, settings.Language, BuildPrompt(settings));
        var segments = await transcriber.TranscribeAsync(samples, request, cancellationToken).ConfigureAwait(false);

        return new DictationResult(DictationFormatter.Format(segments, settings.VoiceCommands), audio.Duration);
    }

    /// <summary>
    /// Whisper imite le style du « prompt » : une phrase bien ponctuée l'incite à ponctuer,
    /// et le vocabulaire personnalisé l'aide à orthographier les noms propres.
    /// </summary>
    internal static string BuildPrompt(DictationSettings settings)
    {
        var prompt = settings.Language switch
        {
            "fr" => "Bonjour, voici ma dictée. Elle est bien ponctuée, avec des majuscules.",
            "en" => "Hello, here is my dictation. It is well punctuated, with capital letters.",
            _ => string.Empty,
        };

        var words = settings.CustomVocabulary
            .Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length > 0)
            prompt = $"{prompt} {string.Join(", ", words)}.".Trim();

        return prompt;
    }
}
