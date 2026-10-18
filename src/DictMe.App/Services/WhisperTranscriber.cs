using DictMe.Core.Transcription;
using Whisper.net;

namespace DictMe.App.Services;

/// <summary>
/// Transcription locale avec whisper.cpp (via Whisper.net) : gratuit, hors ligne, aucune donnée envoyée.
/// </summary>
public sealed class WhisperTranscriber : ITranscriber, IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private WhisperFactory? _factory;
    private string? _loadedModelPath;

    public async Task<IReadOnlyList<string>> TranscribeAsync(float[] samples, TranscriptionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(request);

        // Un seul traitement à la fois : le modèle occupe déjà beaucoup de mémoire sur un téléphone.
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Le chargement du modèle et l'inférence sont lourds : on quitte le thread d'interface.
            return await Task.Run(async () =>
            {
                var factory = GetFactory(request.ModelPath);
                var builder = factory.CreateBuilder()
                    .WithThreads(Math.Clamp(Environment.ProcessorCount - 1, 1, 8));

                builder = string.IsNullOrWhiteSpace(request.Language) || request.Language == "auto"
                    ? builder.WithLanguageDetection()
                    : builder.WithLanguage(request.Language);

                if (!string.IsNullOrWhiteSpace(request.Prompt))
                    builder = builder.WithPrompt(request.Prompt);

                await using var processor = builder.Build();
                var segments = new List<string>();
                await foreach (var segment in processor.ProcessAsync(samples, cancellationToken).ConfigureAwait(false))
                    segments.Add(segment.Text);
                return (IReadOnlyList<string>)segments;
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private WhisperFactory GetFactory(string modelPath)
    {
        if (_factory is not null && _loadedModelPath == modelPath)
            return _factory;

        if (!File.Exists(modelPath))
            throw new FileNotFoundException("Le modèle de transcription n'est pas téléchargé.", modelPath);

        _factory?.Dispose();
        _factory = WhisperFactory.FromPath(modelPath);
        _loadedModelPath = modelPath;
        return _factory;
    }

    /// <summary>Libère le modèle chargé (par exemple avant de le supprimer).</summary>
    public void Unload()
    {
        _lock.Wait();
        try
        {
            _factory?.Dispose();
            _factory = null;
            _loadedModelPath = null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        _factory?.Dispose();
        _lock.Dispose();
    }
}
