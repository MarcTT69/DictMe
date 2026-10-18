namespace DictMe.Core.Models;

/// <summary>
/// Télécharge et stocke les modèles Whisper sur l'appareil. Une fois téléchargé, tout fonctionne hors ligne.
/// </summary>
public sealed class ModelManager
{
    private readonly HttpClient _http;
    private readonly string _directory;

    public ModelManager(HttpClient http, string directory)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(WhisperModel model) => Path.Combine(_directory, model.FileName);

    public bool IsDownloaded(WhisperModel model)
    {
        var file = new FileInfo(GetPath(model));
        return file.Exists && file.Length > 0;
    }

    /// <summary>
    /// Télécharge le modèle dans un fichier temporaire puis le renomme : un téléchargement interrompu
    /// ne laisse jamais un modèle tronqué à la place du vrai.
    /// </summary>
    /// <param name="progress">Avancement entre 0 et 1.</param>
    public async Task DownloadAsync(WhisperModel model, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        Directory.CreateDirectory(_directory);

        var destination = GetPath(model);
        var partial = destination + ".part";

        try
        {
            using var response = await _http.GetAsync(model.DownloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            long total = response.Content.Headers.ContentLength ?? model.ApproximateSizeBytes;
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var target = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                long received = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    received += read;
                    if (total > 0)
                        progress?.Report(Math.Min(1d, (double)received / total));
                }
            }

            File.Move(partial, destination, overwrite: true);
            progress?.Report(1d);
        }
        catch
        {
            TryDelete(partial);
            throw;
        }
    }

    public void Delete(WhisperModel model) => TryDelete(GetPath(model));

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // Fichier encore ouvert : il sera écrasé au prochain téléchargement.
        }
    }
}
