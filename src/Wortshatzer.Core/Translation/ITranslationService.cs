using Wortshatzer.Core.Words;

namespace Wortshatzer.Core.Translation;

public interface ITranslationService
{
    string ProviderName { get; }

    Task<WordTranslation> TranslateAsync(
        CapturedWord capturedWord,
        CancellationToken cancellationToken = default);
}
