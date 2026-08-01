namespace Wortshatzer.Core.Words;

public sealed class WordTranslation
{
    public CapturedWord CapturedWord { get; }
    public string TranslatedText { get; }
    public DateTimeOffset TranslatedAt { get; }

    public WordTranslation(
        CapturedWord capturedWord,
        string translatedText)
    {
        ArgumentNullException.ThrowIfNull(capturedWord);
        ArgumentException.ThrowIfNullOrWhiteSpace(translatedText);

        CapturedWord = capturedWord;
        TranslatedText = translatedText.Trim();
        TranslatedAt = DateTimeOffset.UtcNow;
    }
}