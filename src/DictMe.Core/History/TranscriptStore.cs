using System.Text.Json;
using System.Text.Json.Serialization;

namespace DictMe.Core.History;

/// <summary>
/// Historique des dictées, stocké localement dans un fichier JSON (rien ne quitte le téléphone).
/// </summary>
public sealed class TranscriptStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<Transcript>? _items;

    public TranscriptStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    public event EventHandler? Changed;

    /// <summary>Les dictées, de la plus récente à la plus ancienne.</summary>
    public async Task<IReadOnlyList<Transcript>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var items = await LoadAsync(cancellationToken).ConfigureAwait(false);
            return items.OrderByDescending(t => t.CreatedAt).ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task AddAsync(Transcript transcript, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        return MutateAsync(items => items.Add(transcript), cancellationToken);
    }

    public Task UpdateTextAsync(Guid id, string text, CancellationToken cancellationToken = default) =>
        MutateAsync(items =>
        {
            int index = items.FindIndex(t => t.Id == id);
            if (index >= 0)
                items[index] = items[index] with { Text = text };
        }, cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        MutateAsync(items => items.RemoveAll(t => t.Id == id), cancellationToken);

    public Task ClearAsync(CancellationToken cancellationToken = default) =>
        MutateAsync(items => items.Clear(), cancellationToken);

    private async Task MutateAsync(Action<List<Transcript>> mutation, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var items = await LoadAsync(cancellationToken).ConfigureAwait(false);
            mutation(items);
            await SaveAsync(items, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private async Task<List<Transcript>> LoadAsync(CancellationToken cancellationToken)
    {
        if (_items is not null)
            return _items;

        if (!File.Exists(_filePath))
            return _items = [];

        try
        {
            await using var stream = File.OpenRead(_filePath);
            _items = await JsonSerializer.DeserializeAsync(stream, HistoryJsonContext.Default.ListTranscript, cancellationToken).ConfigureAwait(false) ?? [];
        }
        catch (JsonException)
        {
            // Fichier corrompu : on le met de côté plutôt que de perdre la possibilité d'enregistrer.
            File.Move(_filePath, _filePath + ".corrupt", overwrite: true);
            _items = [];
        }
        return _items;
    }

    private async Task SaveAsync(List<Transcript> items, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_filePath))!);
        var temp = _filePath + ".tmp";
        await using (var stream = File.Create(temp))
        {
            await JsonSerializer.SerializeAsync(stream, items, HistoryJsonContext.Default.ListTranscript, cancellationToken).ConfigureAwait(false);
        }
        File.Move(temp, _filePath, overwrite: true);
    }
}

// Sérialisation générée à la compilation : compatible avec le « trimming » des builds Release mobiles.
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(List<Transcript>))]
internal sealed partial class HistoryJsonContext : JsonSerializerContext;
