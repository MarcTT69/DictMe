using DictMe.Core;
using DictMe.Core.History;
using DictMe.Core.Text;
using Microsoft.JSInterop;

namespace DictMe.Web.Services;

/// <summary>
/// État de l'application partagé par les onglets : enregistrement, transcription, réglages, historique et modèles.
/// </summary>
public sealed class DictationState(BrowserBridge browser) : IDisposable
{
    public const string IdleStatus = "Touchez le micro pour dicter";
    private const string HistoryKey = "dictme.history";
    private const double MinimumSeconds = 0.3;

    private DotNetObjectReference<DictationState>? _callbacks;
    private List<Transcript> _history = [];
    private readonly HashSet<string> _cachedModels = [];

    public event Action? Changed;

    public bool IsReady { get; private set; }
    public bool IsStandalone { get; private set; }
    public bool IsIos { get; private set; }

    public DictationSettings Settings { get; private set; } = new();
    public WebModel SelectedModel => WebModelCatalog.FindOrDefault(Settings.ModelId);

    public bool IsRecording { get; private set; }
    public bool IsTranscribing { get; private set; }
    public DateTime RecordingStartedAt { get; private set; }
    public string Status { get; private set; } = IdleStatus;

    public string TranscriptText { get; private set; } = string.Empty;
    private Guid? _currentTranscriptId;

    public string? DownloadingModelId { get; private set; }
    public double DownloadProgress { get; private set; }
    public string? ModelError { get; private set; }

    public IReadOnlyList<Transcript> History => _history;

    public bool IsModelCached(WebModel model) => _cachedModels.Contains(model.RepositoryId);

    public async Task InitializeAsync()
    {
        if (IsReady)
            return;

        _callbacks = DotNetObjectReference.Create(this);
        await browser.InitializeAsync(_callbacks);
        IsStandalone = browser.IsStandalone();
        IsIos = browser.IsIos();
        Settings = LoadSettings();
        browser.Configure(Settings.AutoCopy);
        _history = TranscriptSerializer.Deserialize(browser.GetItem(HistoryKey))
            .OrderByDescending(t => t.CreatedAt).ToList();
        await RefreshCachedModelsAsync();
        IsReady = true;
        NotifyChanged();
    }

    // ---------- Dictée ----------

    /// <summary>Appelé par le JavaScript quand on touche le micro (voir dictme.js).</summary>
    [JSInvokable]
    public Task OnRecordTapped() => IsRecording ? StopAndTranscribeAsync() : StartRecordingAsync();

    private async Task StartRecordingAsync()
    {
        if (IsTranscribing)
            return;

        var error = await browser.StartRecordingAsync();
        if (error is not null)
        {
            Status = error switch
            {
                "permission" => IsIos
                    ? "Accès au micro refusé. Réglages de l'iPhone → Safari → Micro → Autoriser, puis rechargez."
                    : "Accès au micro refusé. Autorisez-le dans les réglages du navigateur.",
                "unsupported" => "Ce navigateur ne permet pas d'enregistrer. Utilisez Safari (iOS 15 ou plus récent).",
                _ => "Impossible d'accéder au micro.",
            };
            NotifyChanged();
            return;
        }

        IsRecording = true;
        RecordingStartedAt = DateTime.UtcNow;
        Status = "J'écoute… touchez pour terminer";
        NotifyChanged();
    }

    private async Task StopAndTranscribeAsync()
    {
        IsRecording = false;
        IsTranscribing = true;
        Status = "Préparation de l'audio…";
        NotifyChanged();

        var settings = Settings;
        var model = SelectedModel;
        bool copied = false;
        try
        {
            double seconds = await browser.StopRecordingAsync();
            if (seconds < MinimumSeconds)
            {
                Status = "Enregistrement trop court.";
                return;
            }

            Status = IsModelCached(model)
                ? "Transcription en cours…"
                : $"Premier lancement : téléchargement du modèle {model.DisplayName} ({model.SizeLabel})…";
            NotifyChanged();

            var segments = await browser.TranscribeAsync(model.RepositoryId, settings.Language);
            _cachedModels.Add(model.RepositoryId);
            DownloadingModelId = null;

            var text = DictationFormatter.Format(segments, settings.VoiceCommands);
            if (string.IsNullOrWhiteSpace(text))
            {
                Status = "Je n'ai rien entendu. Réessayez en parlant plus près du micro.";
                return;
            }

            var transcript = new Transcript(Guid.NewGuid(), DateTimeOffset.Now, text,
                TimeSpan.FromSeconds(seconds), settings.Language, model.Id);
            _history.Insert(0, transcript);
            SaveHistory();
            _currentTranscriptId = transcript.Id;
            TranscriptText = text;

            if (settings.AutoCopy)
            {
                copied = await browser.CompleteClipboardAsync(text);
                Status = copied ? "Copié dans le presse-papiers ✓" : "Touchez « Copier » pour copier le texte.";
            }
            else
            {
                Status = IdleStatus;
            }
        }
        catch (JSException ex)
        {
            Status = $"La transcription a échoué : {FirstLine(ex.Message)}";
        }
        finally
        {
            if (!copied)
                browser.CancelClipboard();
            IsTranscribing = false;
            DownloadingModelId = null;
            NotifyChanged();
        }
    }

    public void EditTranscript(string text)
    {
        TranscriptText = text;
        if (_currentTranscriptId is { } id)
            UpdateHistoryText(id, text);
    }

    public void ClearTranscript()
    {
        TranscriptText = string.Empty;
        _currentTranscriptId = null;
        Status = IdleStatus;
        NotifyChanged();
    }

    public void Copy(string text)
    {
        browser.CopyText(text);
        Status = "Copié dans le presse-papiers ✓";
        NotifyChanged();
    }

    public void Share(string text)
    {
        if (!browser.Share(text))
            Copy(text);
    }

    // ---------- Historique ----------

    public void DeleteTranscript(Guid id)
    {
        _history.RemoveAll(t => t.Id == id);
        if (_currentTranscriptId == id)
            _currentTranscriptId = null;
        SaveHistory();
        NotifyChanged();
    }

    public void ClearHistory()
    {
        _history.Clear();
        _currentTranscriptId = null;
        SaveHistory();
        NotifyChanged();
    }

    private void UpdateHistoryText(Guid id, string text)
    {
        int index = _history.FindIndex(t => t.Id == id);
        if (index < 0)
            return;
        _history[index] = _history[index] with { Text = text };
        SaveHistory();
    }

    private void SaveHistory() => browser.SetItem(HistoryKey, TranscriptSerializer.Serialize(_history));

    // ---------- Modèles ----------

    [JSInvokable]
    public void OnModelProgress(string repositoryId, double percent)
    {
        DownloadingModelId = repositoryId;
        DownloadProgress = Math.Clamp(percent / 100d, 0, 1);
        if (IsTranscribing)
            Status = $"Téléchargement du modèle… {DownloadProgress:P0}";
        NotifyChanged();
    }

    public async Task DownloadModelAsync(WebModel model)
    {
        ModelError = null;
        DownloadingModelId = model.RepositoryId;
        DownloadProgress = 0;
        NotifyChanged();
        try
        {
            await browser.LoadModelAsync(model.RepositoryId);
            _cachedModels.Add(model.RepositoryId);
            if (!IsModelCached(SelectedModel))
                SelectModel(model);
        }
        catch (JSException ex)
        {
            ModelError = $"Échec du téléchargement de {model.DisplayName} : {FirstLine(ex.Message)}";
        }
        finally
        {
            DownloadingModelId = null;
            NotifyChanged();
        }
    }

    public async Task DeleteModelAsync(WebModel model)
    {
        await browser.DeleteModelAsync(model.RepositoryId);
        await RefreshCachedModelsAsync();
        NotifyChanged();
    }

    private async Task RefreshCachedModelsAsync()
    {
        var cached = await browser.GetCachedModelsAsync(WebModelCatalog.All.Select(m => m.RepositoryId));
        _cachedModels.Clear();
        _cachedModels.UnionWith(cached);
    }

    // ---------- Réglages ----------

    public void SelectModel(WebModel model) => UpdateSettings(Settings with { ModelId = model.Id });

    public void UpdateSettings(DictationSettings settings)
    {
        Settings = settings;
        browser.SetItem("dictme.language", settings.Language);
        browser.SetItem("dictme.model", settings.ModelId);
        browser.SetItem("dictme.voiceCommands", settings.VoiceCommands ? "1" : "0");
        browser.SetItem("dictme.autoCopy", settings.AutoCopy ? "1" : "0");
        browser.Configure(settings.AutoCopy);
        NotifyChanged();
    }

    private DictationSettings LoadSettings()
    {
        var defaults = new DictationSettings { ModelId = WebModelCatalog.Default.Id };
        return defaults with
        {
            Language = browser.GetItem("dictme.language") ?? defaults.Language,
            ModelId = browser.GetItem("dictme.model") ?? defaults.ModelId,
            VoiceCommands = (browser.GetItem("dictme.voiceCommands") ?? "1") == "1",
            AutoCopy = (browser.GetItem("dictme.autoCopy") ?? "1") == "1",
        };
    }

    private void NotifyChanged() => Changed?.Invoke();

    // Les erreurs JavaScript arrivent avec leur pile d'appels : seule la première ligne intéresse l'utilisateur.
    private static string FirstLine(string message) => message.Split('\n', 2)[0].Trim();

    public void Dispose() => _callbacks?.Dispose();
}
