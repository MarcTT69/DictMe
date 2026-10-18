using Microsoft.JSInterop;

namespace DictMe.Web.Services;

/// <summary>Accès typé au module JavaScript <c>js/dictme.js</c>.</summary>
public sealed class BrowserBridge(IJSRuntime js) : IAsyncDisposable
{
    private IJSInProcessObjectReference? _module;

    private IJSInProcessObjectReference Module =>
        _module ?? throw new InvalidOperationException("BrowserBridge n'est pas initialisé.");

    public async Task InitializeAsync<T>(DotNetObjectReference<T> callbacks) where T : class
    {
        _module ??= (IJSInProcessObjectReference)await js.InvokeAsync<IJSObjectReference>("import", "./js/dictme.js");
        Module.InvokeVoid("init", callbacks);
    }

    public void Configure(bool autoCopy) => Module.InvokeVoid("configure", new { autoCopy });

    public bool IsStandalone() => Module.Invoke<bool>("isStandalone");

    public bool IsIos() => Module.Invoke<bool>("isIos");

    /// <returns>null si l'enregistrement a démarré, sinon un code d'erreur ("permission", "microphone", "unsupported").</returns>
    public ValueTask<string?> StartRecordingAsync() => Module.InvokeAsync<string?>("startRecording");

    /// <returns>Durée de l'enregistrement en secondes.</returns>
    public ValueTask<double> StopRecordingAsync() => Module.InvokeAsync<double>("stopRecording");

    public ValueTask<string[]> TranscribeAsync(string repositoryId, string language) =>
        Module.InvokeAsync<string[]>("transcribe", TimeSpan.FromMinutes(30), [repositoryId, language]);

    public ValueTask LoadModelAsync(string repositoryId) =>
        Module.InvokeVoidAsync("loadModel", TimeSpan.FromMinutes(30), [repositoryId]);

    public ValueTask<string[]> GetCachedModelsAsync(IEnumerable<string> repositoryIds) =>
        Module.InvokeAsync<string[]>("cachedModels", [repositoryIds.ToArray()]);

    public ValueTask DeleteModelAsync(string repositoryId) => Module.InvokeVoidAsync("deleteModel", repositoryId);

    public ValueTask<bool> CompleteClipboardAsync(string text) => Module.InvokeAsync<bool>("completeClipboard", text);

    public void CancelClipboard() => Module.InvokeVoid("cancelClipboard");

    // Appels synchrones : ils s'exécutent pendant le toucher de l'utilisateur, condition imposée par Safari.
    public void CopyText(string text) => Module.InvokeVoid("copyText", text);

    public bool Share(string text) => Module.Invoke<bool>("share", text);

    public string? GetItem(string key) => Module.Invoke<string?>("getItem", key);

    public void SetItem(string key, string value) => Module.InvokeVoid("setItem", key, value);

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
            await _module.DisposeAsync();
    }
}
