namespace DictMe.Core.Transcription;

public sealed record TranscriptionRequest(
    string ModelPath,
    string Language,
    string? Prompt = null);

public interface ITranscriber
{
    /// <summary>Transcrit de l'audio mono 16 kHz et renvoie les segments de texte bruts.</summary>
    Task<IReadOnlyList<string>> TranscribeAsync(float[] samples, TranscriptionRequest request, CancellationToken cancellationToken = default);
}
