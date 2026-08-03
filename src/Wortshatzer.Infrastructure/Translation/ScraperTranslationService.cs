using Wortshatzer.Core.Dictionary;
using Wortshatzer.Core.Translation;
using Wortshatzer.Core.Words;

namespace Wortshatzer.Infrastructure.Translation;

public sealed class ScraperTranslationService : ITranslationService
{
    private readonly IScraperProfileResolver _profileResolver;
    private readonly IDictionaryLookupService _lookupService;

    public string ProviderName => "Web scraper";

    public ScraperTranslationService(
        IScraperProfileResolver profileResolver,
        IDictionaryLookupService lookupService)
    {
        ArgumentNullException.ThrowIfNull(profileResolver);
        ArgumentNullException.ThrowIfNull(lookupService);

        _profileResolver = profileResolver;
        _lookupService = lookupService;
    }

    public async Task<WordTranslation> TranslateAsync(
        CapturedWord capturedWord,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capturedWord);

        var languagePair = capturedWord.LanguagePair;

        try
        {
            var profile = await _profileResolver.ResolveAsync(
                languagePair.Source.Code,
                languagePair.Target.Code,
                cancellationToken);

            if (profile is null)
            {
                throw new TranslationException(
                    $"No web-scraper profile is configured for {languagePair.Source.DisplayName} → {languagePair.Target.DisplayName}. Open Dictionary settings to create or select one.");
            }

            var result = await _lookupService.LookupAsync(
                profile,
                capturedWord.Text,
                cancellationToken);
            var translatedText = result
                .GetValues(DictionaryField.Translation)
                .FirstOrDefault(value =>
                    !string.IsNullOrWhiteSpace(value));

            if (string.IsNullOrWhiteSpace(translatedText))
            {
                throw new TranslationException(
                    $"The active profile '{profile.Name}' returned no Translation value. Add a Translation field in Dictionary settings or choose another method.");
            }

            return new WordTranslation(
                capturedWord,
                translatedText);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TranslationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new TranslationException(
                $"Web-scraper translation failed: {exception.Message}",
                exception);
        }
    }
}
