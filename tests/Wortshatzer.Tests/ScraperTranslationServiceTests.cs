using Wortshatzer.Core.Dictionary;
using Wortshatzer.Core.Languages;
using Wortshatzer.Core.Translation;
using Wortshatzer.Core.Words;
using Wortshatzer.Infrastructure.Translation;
using Xunit;

namespace Wortshatzer.Tests;

public sealed class ScraperTranslationServiceTests
{
    [Fact]
    public async Task TranslateAsync_UsesTranslationFromActiveProfile()
    {
        var profile = CreateProfile();
        var result = new DictionaryLookupResult(
            "vielleicht",
            profile.Name,
            new Uri("https://dictionary.test/vielleicht"),
            new Dictionary<string, IReadOnlyList<string>>
            {
                [DictionaryField.Translation.ToString()] =
                    ["maybe", "perhaps"]
            });
        var service = new ScraperTranslationService(
            new StubProfileResolver(profile),
            new StubLookupService(result));

        var translation = await service.TranslateAsync(
            CreateCapturedWord(),
            TestContext.Current.CancellationToken);

        Assert.Equal("maybe", translation.TranslatedText);
        Assert.Equal(
            "Web scraper",
            service.ProviderName);
    }

    [Fact]
    public async Task TranslateAsync_ExplainsMissingLanguageProfile()
    {
        var service = new ScraperTranslationService(
            new StubProfileResolver(null),
            new StubLookupService(null));

        var exception = await Assert.ThrowsAsync<TranslationException>(
            () => service.TranslateAsync(
                CreateCapturedWord(),
                TestContext.Current.CancellationToken));

        Assert.Contains(
            "No web-scraper profile",
            exception.Message);
        Assert.Contains(
            "Dictionary settings",
            exception.Message);
    }

    [Fact]
    public async Task TranslateAsync_ExplainsMissingTranslationField()
    {
        var profile = CreateProfile();
        var result = new DictionaryLookupResult(
            "vielleicht",
            profile.Name,
            new Uri("https://dictionary.test/vielleicht"),
            new Dictionary<string, IReadOnlyList<string>>
            {
                [DictionaryField.Definition.ToString()] =
                    ["Used when something is possible."]
            });
        var service = new ScraperTranslationService(
            new StubProfileResolver(profile),
            new StubLookupService(result));

        var exception = await Assert.ThrowsAsync<TranslationException>(
            () => service.TranslateAsync(
                CreateCapturedWord(),
                TestContext.Current.CancellationToken));

        Assert.Contains(
            "returned no Translation value",
            exception.Message);
    }

    private static CapturedWord CreateCapturedWord()
    {
        return new CapturedWord(
            "vielleicht",
            new LanguagePair(
                new Language("de", "German"),
                new Language("en", "English")));
    }

    private static ScraperProfile CreateProfile()
    {
        return new ScraperProfile(
            "Test dictionary",
            "https://dictionary.test/{word}",
            "de",
            "en",
            [
                new ScraperExtractionRule(
                    DictionaryField.Translation,
                    ".translation")
            ]);
    }

    private sealed class StubProfileResolver
        : IScraperProfileResolver
    {
        private readonly ScraperProfile? _profile;

        public StubProfileResolver(
            ScraperProfile? profile)
        {
            _profile = profile;
        }

        public Task<ScraperProfile?> ResolveAsync(
            string sourceLanguageCode,
            string targetLanguageCode,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_profile);
        }
    }

    private sealed class StubLookupService
        : IDictionaryLookupService
    {
        private readonly DictionaryLookupResult? _result;

        public StubLookupService(
            DictionaryLookupResult? result)
        {
            _result = result;
        }

        public Task<DictionaryLookupResult> LookupAsync(
            ScraperProfile profile,
            string word,
            CancellationToken cancellationToken = default)
        {
            if (_result is null)
            {
                throw new InvalidOperationException(
                    "Lookup should not run without a profile.");
            }

            return Task.FromResult(_result);
        }
    }
}
