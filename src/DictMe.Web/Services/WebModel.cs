namespace DictMe.Web.Services;

/// <summary>Modèle Whisper au format ONNX, exécuté dans le navigateur par transformers.js.</summary>
public sealed record WebModel(string Id, string DisplayName, string RepositoryId, string SizeLabel, string Description);

public static class WebModelCatalog
{
    public static IReadOnlyList<WebModel> All { get; } =
    [
        new("tiny", "Tiny", "onnx-community/whisper-tiny", "~40 Mo",
            "Le plus rapide. Qualité correcte pour des phrases simples."),
        new("base", "Base", "onnx-community/whisper-base", "~80 Mo",
            "Bon compromis vitesse / qualité. Recommandé sur iPhone."),
        new("small", "Small", "onnx-community/whisper-small", "~250 Mo",
            "Le plus précis, mais lent et gourmand en mémoire. Pour les iPhone récents."),
    ];

    public static WebModel Default => All[1];

    public static WebModel FindOrDefault(string? id) =>
        All.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase)) ?? Default;
}
