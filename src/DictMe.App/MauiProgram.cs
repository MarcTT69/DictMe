using DictMe.App.Services;
using DictMe.App.ViewModels;
using DictMe.App.Views;
using DictMe.Core.History;
using DictMe.Core.Models;
using DictMe.Core.Transcription;
using Microsoft.Extensions.Logging;
using Plugin.Maui.Audio;

namespace DictMe.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        var dataDirectory = FileSystem.AppDataDirectory;

        // Les modèles font jusqu'à ~200 Mo : on laisse le temps au téléchargement d'aboutir.
        builder.Services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromMinutes(30) });
        builder.Services.AddSingleton(sp => new ModelManager(sp.GetRequiredService<HttpClient>(), Path.Combine(dataDirectory, "models")));
        builder.Services.AddSingleton(_ => new TranscriptStore(Path.Combine(dataDirectory, "history.json")));
        builder.Services.AddSingleton<WhisperTranscriber>();
        builder.Services.AddSingleton<ITranscriber>(sp => sp.GetRequiredService<WhisperTranscriber>());
        builder.Services.AddSingleton<DictationPipeline>();

        builder.Services.AddSingleton(AudioManager.Current);
        builder.Services.AddSingleton<RecordingService>();
        builder.Services.AddSingleton<SettingsService>();

        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<HistoryViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>();

        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddSingleton<HistoryPage>();
        builder.Services.AddSingleton<SettingsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
