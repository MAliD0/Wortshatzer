using Wortshatzer.Core.Languages;

namespace Wortshatzer.Core.Words;

public sealed class CapturedWord
{
    public Guid Id { get; }
    public string Text { get; }
    public LanguagePair LanguagePair { get; }
    public DateTimeOffset CapturedAt { get; }

    public CapturedWord(string text, LanguagePair languagePair)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(languagePair);

        Id = Guid.NewGuid();
        Text = text.Trim();
        LanguagePair = languagePair;
        CapturedAt = DateTimeOffset.UtcNow;
    }
}