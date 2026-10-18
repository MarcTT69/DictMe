namespace DictMe.Core.Models;

/// <summary>Un modèle Whisper (format ggml) téléchargeable.</summary>
public sealed record WhisperModel(
    string Id,
    string DisplayName,
    string FileName,
    long ApproximateSizeBytes,
    string Description)
{
    public const string DownloadBaseUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/";

    public Uri DownloadUri => new(DownloadBaseUrl + FileName);

    public string SizeLabel => ApproximateSizeBytes >= 1_000_000_000
        ? $"{ApproximateSizeBytes / 1_000_000_000d:0.#} Go"
        : $"{ApproximateSizeBytes / 1_000_000d:0} Mo";
}

public static class ModelCatalog
{
    // Versions quantifiées (q5_1) : 3 à 4 fois plus légères, qualité quasi identique, bien plus rapides sur téléphone.
    public static readonly WhisperModel Tiny = new(
        "tiny", "Tiny", "ggml-tiny-q5_1.bin", 32_200_000,
        "Ultra rapide, qualité correcte. Idéal pour les téléphones anciens.");

    public static readonly WhisperModel Base = new(
        "base", "Base", "ggml-base-q5_1.bin", 59_700_000,
        "Bon compromis vitesse / qualité. Recommandé pour commencer.");

    public static readonly WhisperModel Small = new(
        "small", "Small", "ggml-small-q5_1.bin", 190_000_000,
        "Meilleure précision (surtout en français). Pour les téléphones récents.");

    public static IReadOnlyList<WhisperModel> All { get; } = [Tiny, Base, Small];

    public static WhisperModel Default => Base;

    public static WhisperModel FindOrDefault(string? id) =>
        All.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase)) ?? Default;
}
