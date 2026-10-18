using DictMe.Core.Text;

namespace DictMe.Core.Tests;

public class DictationFormatterTests
{
    [Fact]
    public void Joins_segments_and_trims_spaces()
    {
        var text = DictationFormatter.Format([" Bonjour à tous.", "  Comment allez-vous ? "], applyVoiceCommands: false);

        Assert.Equal("Bonjour à tous. Comment allez-vous?", text);
    }

    [Theory]
    [InlineData("[BLANK_AUDIO]")]
    [InlineData("(Musique)")]
    [InlineData("*rires*")]
    [InlineData("Sous-titres réalisés par la communauté d'Amara.org")]
    public void Drops_annotations_and_silence_hallucinations(string segment)
    {
        Assert.Equal("Ok.", DictationFormatter.Format(["Ok.", segment], applyVoiceCommands: false));
    }

    [Fact]
    public void Keeps_parentheses_inside_real_sentences()
    {
        Assert.Equal("Le projet (version 2) avance.",
            DictationFormatter.Format("Le projet (version 2) avance.", applyVoiceCommands: false));
    }

    [Theory]
    [InlineData("Bonjour, virgule, ça va", "Bonjour, ça va")]
    [InlineData("Tu viens point d'interrogation", "Tu viens?")]
    [InlineData("Génial. Point d'exclamation.", "Génial!")]
    [InlineData("Première ligne. À la ligne. Deuxième ligne.", "Première ligne.\nDeuxième ligne.")]
    [InlineData("Titre, nouveau paragraphe, le texte commence", "Titre\n\nLe texte commence")]
    [InlineData("C'est fini point à la ligne ensuite on continue", "C'est fini.\nEnsuite on continue")]
    [InlineData("Les courses: du pain point-virgule du lait", "Les courses: du pain; du lait")]
    [InlineData("Hello comma new line world", "Hello,\nWorld")]
    public void Applies_voice_commands(string input, string expected)
    {
        Assert.Equal(expected, DictationFormatter.Format(input, applyVoiceCommands: true));
    }

    [Fact]
    public void Leaves_commands_untouched_when_disabled()
    {
        Assert.Equal("Bonjour virgule ça va",
            DictationFormatter.Format("bonjour virgule ça va", applyVoiceCommands: false));
    }

    [Fact]
    public void Does_not_replace_words_containing_a_command()
    {
        Assert.Equal("Les virgules flottantes.",
            DictationFormatter.Format("Les virgules flottantes.", applyVoiceCommands: true));
    }

    [Fact]
    public void Collapses_repeated_punctuation_and_keeps_ellipsis()
    {
        Assert.Equal("Attends… Quoi? Vraiment.",
            DictationFormatter.Format("attends... quoi ?. vraiment..", applyVoiceCommands: false));
    }
}
