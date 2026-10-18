using DictMe.Core;

namespace DictMe.App.Services;

/// <summary>Réglages persistés dans les préférences de l'appareil.</summary>
public sealed class SettingsService
{
    private readonly IPreferences _preferences = Preferences.Default;

    public event EventHandler? Changed;

    public DictationSettings Load()
    {
        var defaults = new DictationSettings();
        return new DictationSettings
        {
            Language = _preferences.Get(nameof(DictationSettings.Language), defaults.Language),
            ModelId = _preferences.Get(nameof(DictationSettings.ModelId), defaults.ModelId),
            VoiceCommands = _preferences.Get(nameof(DictationSettings.VoiceCommands), defaults.VoiceCommands),
            AutoCopy = _preferences.Get(nameof(DictationSettings.AutoCopy), defaults.AutoCopy),
            CustomVocabulary = _preferences.Get(nameof(DictationSettings.CustomVocabulary), defaults.CustomVocabulary),
        };
    }

    public void Save(DictationSettings settings)
    {
        _preferences.Set(nameof(DictationSettings.Language), settings.Language);
        _preferences.Set(nameof(DictationSettings.ModelId), settings.ModelId);
        _preferences.Set(nameof(DictationSettings.VoiceCommands), settings.VoiceCommands);
        _preferences.Set(nameof(DictationSettings.AutoCopy), settings.AutoCopy);
        _preferences.Set(nameof(DictationSettings.CustomVocabulary), settings.CustomVocabulary);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Update(Func<DictationSettings, DictationSettings> change) => Save(change(Load()));
}
