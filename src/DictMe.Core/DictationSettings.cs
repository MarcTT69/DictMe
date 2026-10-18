namespace DictMe.Core;

public sealed record DictationSettings
{
    /// <summary>Code langue Whisper ("fr", "en"...) ou "auto" pour la détection automatique.</summary>
    public string Language { get; init; } = "fr";

    public string ModelId { get; init; } = Models.ModelCatalog.Default.Id;

    /// <summary>Interpréter "à la ligne", "virgule", "point d'interrogation"...</summary>
    public bool VoiceCommands { get; init; } = true;

    /// <summary>Copier automatiquement le texte dans le presse-papiers après la transcription.</summary>
    public bool AutoCopy { get; init; } = true;

    /// <summary>Noms propres, jargon... séparés par des virgules, pour aider Whisper à bien les écrire.</summary>
    public string CustomVocabulary { get; init; } = string.Empty;
}
