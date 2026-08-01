using Wortshatzer.Core.Translation;
using Wortshatzer.Core.Words;

namespace Wortshatzer.Infrastructure.Translation;

public sealed class InMemoryTranslationService : ITranslationService
{
    private readonly Dictionary<(string Source, string Target, string Word), string> _translations = new()
    {
        [("de", "en", "vielleicht")] = "maybe",
        [("de", "en", "hallo")] = "hello",
        [("de", "en", "haus")] = "house",
        [("de", "en", "lernen")] = "learn",
        [("de", "en", "wort")] = "word",
        [("en", "de", "maybe")] = "vielleicht",
        [("en", "de", "hello")] = "hallo",
        [("en", "de", "house")] = "Haus",
        [("pl", "en", "cześć")] = "hello",
        [("pl", "en", "dom")] = "house",
        [("ru", "en", "привет")] = "hello",
        [("ru", "en", "дом")] = "house"
    };

    public Task<WordTranslation> TranslateAsync(
        CapturedWord capturedWord,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capturedWord);
        cancellationToken.ThrowIfCancellationRequested();

        var key = (
            capturedWord.LanguagePair.Source.Code,
            capturedWord.LanguagePair.Target.Code,
            capturedWord.Text.ToLowerInvariant());

        if (!_translations.TryGetValue(key, out var translatedText))
        {
            throw new InvalidOperationException(
                $"The demo dictionary does not contain a translation for '{capturedWord.Text}' " +
                $"from {capturedWord.LanguagePair.Source.DisplayName} " +
                $"to {capturedWord.LanguagePair.Target.DisplayName}.");
        }

        return Task.FromResult(new WordTranslation(capturedWord, translatedText));
    }
}
