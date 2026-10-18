using System.Net;
using DictMe.Core.Models;
using DictMe.Core.Transcription;

namespace DictMe.Core.Tests;

public class PipelineTests
{
    private sealed class FakeTranscriber(params string[] segments) : ITranscriber
    {
        public float[]? Samples { get; private set; }
        public TranscriptionRequest? Request { get; private set; }

        public Task<IReadOnlyList<string>> TranscribeAsync(float[] samples, TranscriptionRequest request, CancellationToken cancellationToken = default)
        {
            Samples = samples;
            Request = request;
            return Task.FromResult<IReadOnlyList<string>>(segments);
        }
    }

    [Fact]
    public async Task Converts_audio_transcribes_and_formats()
    {
        var transcriber = new FakeTranscriber(" bonjour virgule", " à la ligne. Au revoir.", "[BLANK_AUDIO]");
        var wav = WavBuilder.Pcm16(new short[44100], 44100, 1);
        var settings = new DictationSettings { Language = "fr", CustomVocabulary = "DictMe, Whisper" };

        var result = await new DictationPipeline(transcriber).ProcessAsync(new MemoryStream(wav), "/model.bin", settings);

        Assert.Equal("Bonjour,\nAu revoir.", result.Text);
        Assert.Equal(TimeSpan.FromSeconds(1), result.AudioDuration);
        Assert.Equal(16000, transcriber.Samples!.Length);
        Assert.Equal("fr", transcriber.Request!.Language);
        Assert.Equal("/model.bin", transcriber.Request.ModelPath);
        Assert.EndsWith("DictMe, Whisper.", transcriber.Request.Prompt);
    }

    [Fact]
    public async Task Skips_transcription_for_too_short_recordings()
    {
        var transcriber = new FakeTranscriber("ne doit pas apparaître");
        var wav = WavBuilder.Pcm16(new short[1600], 16000, 1);

        var result = await new DictationPipeline(transcriber).ProcessAsync(new MemoryStream(wav), "/m", new DictationSettings());

        Assert.Equal(string.Empty, result.Text);
        Assert.Null(transcriber.Samples);
    }

    [Fact]
    public void Prompt_without_known_language_is_vocabulary_only()
    {
        var prompt = DictationPipeline.BuildPrompt(new DictationSettings { Language = "auto", CustomVocabulary = " Kubernetes ;\nMAUI " });

        Assert.Equal("Kubernetes, MAUI.", prompt);
    }
}

public sealed class ModelManagerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "dictme-models-" + Guid.NewGuid());

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public Uri? LastUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri;
            return Task.FromResult(respond(request));
        }
    }

    [Fact]
    public async Task Downloads_model_and_reports_progress()
    {
        var payload = new byte[200_000];
        Random.Shared.NextBytes(payload);
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) });
        var manager = new ModelManager(new HttpClient(handler), _directory);
        var reports = new List<double>();

        Assert.False(manager.IsDownloaded(ModelCatalog.Tiny));
        await manager.DownloadAsync(ModelCatalog.Tiny, new SyncProgress(reports.Add));

        Assert.True(manager.IsDownloaded(ModelCatalog.Tiny));
        Assert.Equal(payload, await File.ReadAllBytesAsync(manager.GetPath(ModelCatalog.Tiny)));
        Assert.Equal(1d, reports[^1]);
        Assert.Equal(new Uri("https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny-q5_1.bin"), handler.LastUri);

        manager.Delete(ModelCatalog.Tiny);
        Assert.False(manager.IsDownloaded(ModelCatalog.Tiny));
    }

    [Fact]
    public async Task Failed_download_leaves_no_file()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var manager = new ModelManager(new HttpClient(handler), _directory);

        await Assert.ThrowsAsync<HttpRequestException>(() => manager.DownloadAsync(ModelCatalog.Base));

        Assert.False(manager.IsDownloaded(ModelCatalog.Base));
        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public void Catalog_falls_back_to_default()
    {
        Assert.Same(ModelCatalog.Small, ModelCatalog.FindOrDefault("SMALL"));
        Assert.Same(ModelCatalog.Default, ModelCatalog.FindOrDefault("inconnu"));
        Assert.Equal("190 Mo", ModelCatalog.Small.SizeLabel);
    }

    private sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
