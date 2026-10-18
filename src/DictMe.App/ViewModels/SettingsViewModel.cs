using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DictMe.App.Services;
using DictMe.Core.Models;
using DictMe.Core.Transcription;

namespace DictMe.App.ViewModels;

public sealed record LanguageOption(string Code, string Name)
{
    // Le Picker affiche ToString() : pas besoin de liaison sur un autre type que la page.
    public override string ToString() => Name;
}

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private bool _loading;

    public SettingsViewModel(SettingsService settings, ModelManager models, WhisperTranscriber transcriber)
    {
        _settings = settings;
        Models = new(ModelCatalog.All.Select(m => new ModelItemViewModel(m, models, transcriber, this)));
        Load();
    }

    public ObservableCollection<ModelItemViewModel> Models { get; }

    public IReadOnlyList<LanguageOption> Languages { get; } =
    [
        new("fr", "Français"),
        new("en", "Anglais"),
        new("es", "Espagnol"),
        new("de", "Allemand"),
        new("it", "Italien"),
        new("pt", "Portugais"),
        new("auto", "Détection automatique"),
    ];

    [ObservableProperty]
    public partial LanguageOption? SelectedLanguage { get; set; }

    [ObservableProperty]
    public partial bool VoiceCommands { get; set; }

    [ObservableProperty]
    public partial bool AutoCopy { get; set; }

    [ObservableProperty]
    public partial string CustomVocabulary { get; set; } = string.Empty;

    public void Load()
    {
        _loading = true;
        var current = _settings.Load();
        SelectedLanguage = Languages.FirstOrDefault(l => l.Code == current.Language) ?? Languages[0];
        VoiceCommands = current.VoiceCommands;
        AutoCopy = current.AutoCopy;
        CustomVocabulary = current.CustomVocabulary;
        foreach (var item in Models)
            item.Refresh(current.ModelId);
        _loading = false;
    }

    internal void SelectModel(WhisperModel model)
    {
        _settings.Update(s => s with { ModelId = model.Id });
        foreach (var item in Models)
            item.Refresh(model.Id);
    }

    internal string SelectedModelId => _settings.Load().ModelId;

    /// <summary>Après un téléchargement ou une suppression : prévient l'écran de dictée.</summary>
    internal void NotifyModelsChanged() => _settings.Update(s => s);

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (!_loading && value is not null)
            _settings.Update(s => s with { Language = value.Code });
    }

    partial void OnVoiceCommandsChanged(bool value)
    {
        if (!_loading)
            _settings.Update(s => s with { VoiceCommands = value });
    }

    partial void OnAutoCopyChanged(bool value)
    {
        if (!_loading)
            _settings.Update(s => s with { AutoCopy = value });
    }

    partial void OnCustomVocabularyChanged(string value)
    {
        if (!_loading)
            _settings.Update(s => s with { CustomVocabulary = value ?? string.Empty });
    }
}
