using Wortshatzer.Core.Words;

namespace Wortshatzer.Core.Translation;

public interface ITranslationService
{
    Task<WordTranslation> TranslateAsync(
        CapturedWord capturedWord,
        CancellationToken cancellationToken = default);
}
