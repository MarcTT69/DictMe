using System.Text.Json.Serialization;

namespace DictMe.Core.History;

public sealed record Transcript(
    Guid Id,
    DateTimeOffset CreatedAt,
    string Text,
    TimeSpan AudioDuration,
    string Language,
    string ModelId)
{
    [JsonIgnore]
    public string Preview => Text.Length <= 140 ? Text : string.Concat(Text.AsSpan(0, 140).TrimEnd(), "…");
}
