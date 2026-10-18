using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DictMe.Core.History;

namespace DictMe.App.ViewModels;

public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly TranscriptStore _store;

    public HistoryViewModel(TranscriptStore store)
    {
        _store = store;
        _store.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(() => _ = LoadAsync());
    }

    public ObservableCollection<HistoryItemViewModel> Items { get; } = [];

    [ObservableProperty]
    public partial bool IsEmpty { get; set; } = true;

    [RelayCommand]
    public async Task LoadAsync()
    {
        var transcripts = await _store.GetAllAsync();
        Items.Clear();
        foreach (var transcript in transcripts)
            Items.Add(new HistoryItemViewModel(transcript, this));
        IsEmpty = Items.Count == 0;
    }

    [RelayCommand]
    private async Task ClearAllAsync()
    {
        if (Items.Count == 0)
            return;

        bool confirmed = await Shell.Current.DisplayAlertAsync(
            "Tout effacer ?", "Toutes les dictées de l'historique seront supprimées.", "Effacer", "Annuler");
        if (confirmed)
            await _store.ClearAsync();
    }

    internal async Task CopyAsync(Transcript transcript)
    {
        await Clipboard.Default.SetTextAsync(transcript.Text);
        await Shell.Current.DisplayAlertAsync("Copié", "Le texte est dans le presse-papiers.", "OK");
    }

    internal Task ShareAsync(Transcript transcript) =>
        Share.Default.RequestAsync(new ShareTextRequest { Text = transcript.Text, Title = "Partager la dictée" });

    internal Task DeleteAsync(Transcript transcript) => _store.DeleteAsync(transcript.Id);
}

public sealed partial class HistoryItemViewModel(Transcript transcript, HistoryViewModel owner)
{
    public Transcript Transcript { get; } = transcript;

    public string Preview => Transcript.Preview;

    public string Details => $"{Transcript.CreatedAt.LocalDateTime:dd/MM/yyyy HH:mm} · {Transcript.AudioDuration:m\\:ss}";

    [RelayCommand]
    private Task CopyAsync() => owner.CopyAsync(Transcript);

    [RelayCommand]
    private Task ShareAsync() => owner.ShareAsync(Transcript);

    [RelayCommand]
    private Task DeleteAsync() => owner.DeleteAsync(Transcript);
}
