using DictMe.Core.History;

namespace DictMe.Core.Tests;

public sealed class TranscriptStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "dictme-tests-" + Guid.NewGuid());
    private string FilePath => Path.Combine(_directory, "history.json");

    private static Transcript Make(string text, int minutesAgo) =>
        new(Guid.NewGuid(), DateTimeOffset.Now.AddMinutes(-minutesAgo), text, TimeSpan.FromSeconds(3), "fr", "base");

    [Fact]
    public async Task Persists_and_returns_newest_first()
    {
        var store = new TranscriptStore(FilePath);
        await store.AddAsync(Make("ancien", 10));
        await store.AddAsync(Make("récent", 1));

        var reloaded = await new TranscriptStore(FilePath).GetAllAsync();

        Assert.Equal(["récent", "ancien"], reloaded.Select(t => t.Text));
    }

    [Fact]
    public async Task Updates_and_deletes()
    {
        var store = new TranscriptStore(FilePath);
        var first = Make("un", 2);
        var second = Make("deux", 1);
        await store.AddAsync(first);
        await store.AddAsync(second);

        await store.UpdateTextAsync(first.Id, "un corrigé");
        await store.DeleteAsync(second.Id);

        var all = await new TranscriptStore(FilePath).GetAllAsync();
        Assert.Equal("un corrigé", Assert.Single(all).Text);
    }

    [Fact]
    public async Task Recovers_from_corrupted_file()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(FilePath, "{ pas du json");
        var store = new TranscriptStore(FilePath);

        Assert.Empty(await store.GetAllAsync());
        await store.AddAsync(Make("nouveau", 0));
        Assert.Single(await new TranscriptStore(FilePath).GetAllAsync());
        Assert.True(File.Exists(FilePath + ".corrupt"));
    }

    [Fact]
    public async Task Raises_changed_event()
    {
        var store = new TranscriptStore(FilePath);
        int changes = 0;
        store.Changed += (_, _) => changes++;

        await store.AddAsync(Make("x", 0));
        await store.ClearAsync();

        Assert.Equal(2, changes);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
