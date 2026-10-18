using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DictMe.App.Services;
using DictMe.Core.Models;
using DictMe.Core.Transcription;

namespace DictMe.App.ViewModels;

public sealed partial class ModelItemViewModel(
    WhisperModel model,
    ModelManager models,
    WhisperTranscriber transcriber,
    SettingsViewModel owner) : ObservableObject
{
    private CancellationTokenSource? _download;

    public WhisperModel Model { get; } = model;

    public string Title => $"{Model.DisplayName} · {Model.SizeLabel}";

    public string Description => Model.Description;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownload), nameof(CanSelect))]
    public partial bool IsDownloaded { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownload), nameof(CanSelect))]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownload))]
    public partial bool IsDownloading { get; set; }

    [ObservableProperty]
    public partial double Progress { get; set; }

    [ObservableProperty]
    public partial string ProgressText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ErrorText { get; set; } = string.Empty;

    public bool CanDownload => !IsDownloaded && !IsDownloading;

    public bool CanSelect => IsDownloaded && !IsSelected;

    public void Refresh(string selectedModelId)
    {
        IsDownloaded = models.IsDownloaded(Model);
        IsSelected = Model.Id == selectedModelId;
    }

    [RelayCommand]
    private async Task DownloadAsync()
    {
        ErrorText = string.Empty;
        IsDownloading = true;
        Progress = 0;
        ProgressText = "0 %";
        _download = new CancellationTokenSource();

        try
        {
            // Progress<T> repasse sur le thread d'interface pour mettre à jour la barre.
            var progress = new Progress<double>(p =>
            {
                Progress = p;
                ProgressText = $"{p:P0}";
            });
            await models.DownloadAsync(Model, progress, _download.Token);

            // Premier modèle téléchargé, ou aucun autre disponible : on l'utilise directement.
            var selected = ModelCatalog.FindOrDefault(owner.SelectedModelId);
            if (selected == Model || !models.IsDownloaded(selected))
                owner.SelectModel(Model);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ErrorText = $"Échec du téléchargement : {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
            _download.Dispose();
            _download = null;
            Refresh(owner.SelectedModelId);
            owner.NotifyModelsChanged();
        }
    }

    [RelayCommand]
    private void CancelDownload() => _download?.Cancel();

    [RelayCommand]
    private void Select() => owner.SelectModel(Model);

    [RelayCommand]
    private async Task DeleteAsync()
    {
        bool confirmed = await Shell.Current.DisplayAlertAsync(
            "Supprimer le modèle ?", $"{Model.DisplayName} ({Model.SizeLabel}) devra être retéléchargé pour être utilisé.",
            "Supprimer", "Annuler");
        if (!confirmed)
            return;

        transcriber.Unload();
        models.Delete(Model);
        Refresh(owner.SelectedModelId);
        owner.NotifyModelsChanged();
    }
}
