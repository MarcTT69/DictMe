using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DictMe.Core.Text;

/// <summary>
/// Nettoie la sortie brute de Whisper et applique les commandes vocales ("à la ligne", "virgule"...).
/// </summary>
public static partial class DictationFormatter
{
    // Phrases que Whisper « hallucine » sur du silence (issues de ses données d'entraînement).
    private static readonly string[] HallucinationMarkers =
    [
        "amara.org",
        "sous-titres réalisés",
        "sous-titrage st'",
        "sous-titrage société radio-canada",
        "subtitles by the amara",
    ];

    // Ordre important : les commandes les plus longues d'abord ("point à la ligne" avant "à la ligne").
    private static readonly (Regex Pattern, string Replacement)[] VoiceCommands =
    [
        (Command(@"point à la ligne", absorbPeriod: true), ".\n"),
        (Command(@"nouveau paragraphe|new paragraph", absorbPeriod: false), "\n\n"),
        (Command(@"retour à la ligne|à la ligne|nouvelle ligne|new line", absorbPeriod: false), "\n"),
        (Command(@"point d'interrogation|point d’interrogation|question mark"), "? "),
        (Command(@"point d'exclamation|point d’exclamation|exclamation (?:mark|point)"), "! "),
        (Command(@"points de suspension"), "… "),
        (Command(@"point[- ]virgule|semicolon"), "; "),
        (Command(@"point final|full stop"), ". "),
        (Command(@"virgule|comma"), ", "),
    ];

    /// <summary>Assemble les segments Whisper en un texte propre.</summary>
    public static string Format(IEnumerable<string> segments, bool applyVoiceCommands)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var builder = new StringBuilder();
        foreach (var raw in segments)
        {
            var segment = CleanSegment(raw);
            if (segment.Length == 0)
                continue;
            if (builder.Length > 0)
                builder.Append(' ');
            builder.Append(segment);
        }

        var text = builder.ToString();
        if (applyVoiceCommands)
            text = ApplyVoiceCommands(text);

        return Normalize(text);
    }

    public static string Format(string text, bool applyVoiceCommands) =>
        Format([text ?? string.Empty], applyVoiceCommands);

    internal static string CleanSegment(string? segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
            return string.Empty;

        // [BLANK_AUDIO], [Musique], *rires*...
        var text = Annotation().Replace(segment, " ").Trim();

        // Un segment entièrement entre parenthèses est une annotation : "(Musique)", "(applaudissements)".
        if (text.StartsWith('(') && text.EndsWith(')') && text.IndexOf(')') == text.Length - 1)
            return string.Empty;

        var lower = text.ToLowerInvariant();
        if (HallucinationMarkers.Any(lower.Contains))
            return string.Empty;

        return text;
    }

    internal static string ApplyVoiceCommands(string text)
    {
        foreach (var (pattern, replacement) in VoiceCommands)
            text = pattern.Replace(text, replacement);
        return text;
    }

    internal static string Normalize(string text)
    {
        text = text.Replace("\r\n", "\n").Replace("...", "…");
        text = HorizontalSpaces().Replace(text, " ");
        text = SpaceBeforeClosingPunctuation().Replace(text, "$1");
        text = RepeatedPunctuation().Replace(text, "$1");
        text = SpacesAroundNewline().Replace(text, "\n");
        text = TooManyNewlines().Replace(text, "\n\n");
        text = text.Trim(' ', '\n', '\t');
        return CapitalizeSentences(text);
    }

    private static string CapitalizeSentences(string text)
    {
        var chars = text.ToCharArray();
        bool capitalizeNext = true;
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if (char.IsLetter(c))
            {
                if (capitalizeNext)
                    chars[i] = char.ToUpper(c, CultureInfo.CurrentCulture);
                capitalizeNext = false;
            }
            else if (c is '.' or '?' or '!' or '…' or '\n')
            {
                capitalizeNext = true;
            }
            else if (!char.IsWhiteSpace(c) && c is not ('"' or '«' or '»' or '(' or '\'' or '-' or '—'))
            {
                capitalizeNext = false;
            }
        }
        return new string(chars);
    }

    /// <summary>
    /// Une commande vocale, avec la ponctuation que Whisper a pu ajouter autour (",", ".") absorbée :
    /// "Bonjour, virgule, ça va" doit donner "Bonjour, ça va" et non "Bonjour,, ça va".
    /// Pour les retours à la ligne, le point qui précède est conservé ("Fini. À la ligne." garde son point).
    /// </summary>
    private static Regex Command(string alternatives, bool absorbPeriod = true) =>
        new($@"[ \t]*{(absorbPeriod ? "[,.;]" : ",")}?[ \t]*(?<![\p{{L}}\-'’])(?:{alternatives})(?![\p{{L}}\-'’])[ \t]*[,.;:]?[ \t]*",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [GeneratedRegex(@"\[[^\]]*\]|\*[^*]+\*")]
    private static partial Regex Annotation();

    [GeneratedRegex(@"[ \t ]+")]
    private static partial Regex HorizontalSpaces();

    [GeneratedRegex(@"[ \t]+([,.;:!?…])")]
    private static partial Regex SpaceBeforeClosingPunctuation();

    [GeneratedRegex(@"([,.;:!?…])[,.;]+")]
    private static partial Regex RepeatedPunctuation();

    [GeneratedRegex(@"[ \t]*\n[ \t]*")]
    private static partial Regex SpacesAroundNewline();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex TooManyNewlines();
}
