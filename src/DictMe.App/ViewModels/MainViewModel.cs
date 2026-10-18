using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DictMe.App.Services;
using DictMe.Core.History;
using DictMe.Core.Models;
using DictMe.Core.Transcription;

namespace DictMe.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private const string IdleStatus = "Touchez le micro pour dicter";

    private readonly RecordingService _recording;
    private readonly DictationPipeline _pipeline;
    private readonly ModelManager _models;
    private readonly TranscriptStore _history;
    private readonly SettingsService _settings;

    private IDispatcherTimer? _timer;
    private DateTime _recordingStartedAt;
    private Guid? _currentTranscriptId;
    private CancellationTokenSource? _saveDebounce;
    private bool _settingTextFromCode;

    public MainViewModel(RecordingService recording, DictationPipeline pipeline, ModelManager models,
        TranscriptStore history, SettingsService settings)
    {
        _recording = recording;
        _pipeline = pipeline;
        _models = models;
        _history = history;
        _settings = settings;
        _settings.Changed += (_, _) => Refresh();
        Refresh();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordButtonText))]
    [NotifyCanExecuteChangedFor(nameof(ToggleRecordingCommand))]
    public partial bool IsRecording { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleRecordingCommand))]
    public partial bool IsTranscribing { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = IdleStatus;

    [ObservableProperty]
    public partial string ElapsedText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTranscript))]
    [NotifyCanExecuteChangedFor(nameof(CopyCommand), nameof(ShareCommand), nameof(ClearCommand))]
    public partial string TranscriptText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ModelMissing { get; set; }

    [ObservableProperty]
    public partial string ModelMissingText { get; set; } = string.Empty;

    public bool HasTranscript => !string.IsNullOrWhiteSpace(TranscriptText);

    public string RecordButtonText => IsRecording ? "■" : "🎙";

    /// <summary>Met à jour l'état qui dépend des réglages (modèle choisi, téléchargé ou non).</summary>
    public void Refresh()
    {
        var model = ModelCatalog.FindOrDefault(_settings.Load().ModelId);
        ModelMissing = !_models.IsDownloaded(model);
        ModelMissingText = $"Téléchargez le modèle « {model.DisplayName} » ({model.SizeLabel}) une seule fois : ensuite, tout fonctionne hors ligne.";
    }

    private bool CanToggleRecording() => !IsTranscribing;

    [RelayCommand(CanExecute = nameof(CanToggleRecording))]
    private async Task ToggleRecordingAsync()
    {
        if (IsRecording)
            await StopAndTranscribeAsync();
        else
            await StartRecordingAsync();
    }

    private async Task StartRecordingAsync()
    {
        Refresh();
        if (ModelMissing)
        {
            await OpenSettingsAsync();
            return;
        }

        if (!await _recording.EnsurePermissionAsync())
        {
            StatusText = "Autorisez l'accès au micro dans les réglages du téléphone.";
            return;
        }

        try
        {
            await _recording.StartAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Impossible de démarrer l'enregistrement : {ex.Message}";
            return;
        }

        IsRecording = true;
        StatusText = "J'écoute… touchez pour terminer";
        DeviceDisplay.Current.KeepScreenOn = true;
        StartTimer();
    }

    private async Task StopAndTranscribeAsync()
    {
        StopTimer();
        DeviceDisplay.Current.KeepScreenOn = false;
        IsRecording = false;
        IsTranscribing = true;
        StatusText = "Transcription en cours…";

        try
        {
            var settings = _settings.Load();
            var model = ModelCatalog.FindOrDefault(settings.ModelId);

            await using var wav = await _recording.StopAsync();
            var result = await _pipeline.ProcessAsync(wav, _models.GetPath(model), settings);

            if (string.IsNullOrWhiteSpace(result.Text))
            {
                StatusText = "Je n'ai rien entendu. Réessayez en parlant plus près du micro.";
                return;
            }

            var transcript = new Transcript(Guid.NewGuid(), DateTimeOffset.Now, result.Text, result.AudioDuration, settings.Language, model.Id);
            await _history.AddAsync(transcript);
            SetTranscript(transcript.Text, transcript.Id);

            if (settings.AutoCopy)
            {
                await Clipboard.Default.SetTextAsync(result.Text);
                StatusText = "Copié dans le presse-papiers ✓";
            }
            else
            {
                StatusText = IdleStatus;
            }
        }
        catch (Exception ex)
        {
            StatusText = $"La transcription a échoué : {ex.Message}";
        }
        finally
        {
            IsTranscribing = false;
            ElapsedText = string.Empty;
        }
    }

    [RelayCommand(CanExecute = nameof(HasTranscript))]
    private async Task CopyAsync()
    {
        await Clipboard.Default.SetTextAsync(TranscriptText);
        StatusText = "Copié dans le presse-papiers ✓";
    }

    [RelayCommand(CanExecute = nameof(HasTranscript))]
    private Task ShareAsync() =>
        Share.Default.RequestAsync(new ShareTextRequest { Text = TranscriptText, Title = "Partager la dictée" });

    [RelayCommand(CanExecute = nameof(HasTranscript))]
    private void Clear()
    {
        SetTranscript(string.Empty, null);
        StatusText = IdleStatus;
    }

    [RelayCommand]
    private Task OpenSettingsAsync() => Shell.Current.GoToAsync("//settings");

    private void SetTranscript(string text, Guid? id)
    {
        _settingTextFromCode = true;
        _currentTranscriptId = id;
        TranscriptText = text;
        _settingTextFromCode = false;
    }

    // Les corrections faites à la main sont enregistrées dans l'historique (après une courte pause de frappe).
    partial void OnTranscriptTextChanged(string value)
    {
        if (_settingTextFromCode || _currentTranscriptId is not { } id)
            return;

        _saveDebounce?.Cancel();
        var debounce = _saveDebounce = new CancellationTokenSource();
        _ = SaveEditAsync(id, value, debounce.Token);
    }

    private async Task SaveEditAsync(Guid id, string text, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(800, cancellationToken);
            await _history.UpdateTextAsync(id, text, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void StartTimer()
    {
        _recordingStartedAt = DateTime.UtcNow;
        ElapsedText = "00:00";
        _timer ??= CreateTimer();
        _timer.Start();
    }

    private void StopTimer() => _timer?.Stop();

    private IDispatcherTimer CreateTimer()
    {
        var timer = Application.Current!.Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(250);
        timer.Tick += (_, _) => ElapsedText = (DateTime.UtcNow - _recordingStartedAt).ToString(@"mm\:ss");
        return timer;
    }
}
