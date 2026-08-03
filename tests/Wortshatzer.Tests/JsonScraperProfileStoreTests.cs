using Wortshatzer.Core.Dictionary;
using Wortshatzer.Infrastructure.Dictionary;
using Xunit;

namespace Wortshatzer.Tests;

public sealed class JsonScraperProfileStoreTests
{
    [Fact]
    public async Task SaveAndLoadAsync_RoundTripsProfileRules()
    {
        var directory = CreateTemporaryDirectory();
        var filePath = Path.Combine(
            directory,
            "scraper-profiles.json");

        try
        {
            var store = new JsonScraperProfileStore(filePath);
            var profile = new ScraperProfile(
                "My dictionary",
                "https://dictionary.test/{word}",
                "de",
                "en",
                [
                    new ScraperExtractionRule(
                        DictionaryField.Translation,
                        ".translation",
                        isRequired: true,
                        maximumResults: 5,
                        fallbackSelectors:
                        [
                            ".old-translation"
                        ]),
                    new ScraperExtractionRule(
                        DictionaryField.Custom,
                        ".level",
                        customFieldName: "Difficulty")
                ],
                ".entry",
                new ScraperSuggestionRule(
                    ".suggestions a",
                    [".alternative-spellings a"],
                    "https://dictionary.test/spellcheck/?q={word}"));

            await store.SaveAsync(
                [profile],
                TestContext.Current.CancellationToken);
            var loaded = await store.LoadAsync(
                TestContext.Current.CancellationToken);

            var restored = Assert.Single(loaded);
            Assert.Equal(profile.Name, restored.Name);
            Assert.Equal(
                profile.SearchUrlTemplate,
                restored.SearchUrlTemplate);
            Assert.Equal(".entry", restored.EntrySelector);
            Assert.NotNull(restored.SuggestionRule);
            Assert.Equal(
                "https://dictionary.test/spellcheck/?q={word}",
                restored.SuggestionRule.SearchUrlTemplate);
            Assert.Equal(
                [".alternative-spellings a"],
                restored.SuggestionRule.FallbackSelectors);
            Assert.Equal(2, restored.Fields.Count);
            Assert.Equal(
                [".old-translation"],
                restored.Fields[0].FallbackSelectors);
            Assert.True(restored.Fields[0].IsRequired);
            Assert.Equal(
                "Difficulty",
                restored.Fields[1].OutputName);
        }
        finally
        {
            TryDeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task LoadAsync_ReportsInvalidJson()
    {
        var directory = CreateTemporaryDirectory();
        var filePath = Path.Combine(
            directory,
            "scraper-profiles.json");

        try
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(
                filePath,
                "{not-json",
                TestContext.Current.CancellationToken);

            var store = new JsonScraperProfileStore(filePath);

            var exception =
                await Assert.ThrowsAsync<
                    ScraperProfilePersistenceException>(
                    () => store.LoadAsync(
                        TestContext.Current.CancellationToken));

            Assert.Contains(filePath, exception.Message);
        }
        finally
        {
            TryDeleteDirectory(directory);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "wortshatzer-tests",
            Guid.NewGuid().ToString("N"));
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Test cleanup is best-effort.
        }
    }
}
