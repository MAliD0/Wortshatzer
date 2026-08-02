using Wortshatzer.Core.Dictionary;
using Wortshatzer.Infrastructure.Dictionary;
using Xunit;

namespace Wortshatzer.Tests;

public sealed class ActiveScraperProfileTests
{
    [Fact]
    public async Task ActiveProfileStore_RoundTripsLanguagePair()
    {
        var directory = CreateTemporaryDirectory();
        var filePath = Path.Combine(
            directory,
            "active-profiles.json");

        try
        {
            var writer =
                new JsonActiveScraperProfileStore(filePath);

            await writer.SetActiveProfileNameAsync(
                "DE",
                "EN",
                "My dictionary",
                TestContext.Current.CancellationToken);

            var reader =
                new JsonActiveScraperProfileStore(filePath);
            var active =
                await reader.GetActiveProfileNameAsync(
                    "de",
                    "en",
                    TestContext.Current.CancellationToken);

            Assert.Equal("My dictionary", active);
        }
        finally
        {
            TryDeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task Resolver_ReturnsSelectedCustomProfile()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var customStore =
                new JsonScraperProfileStore(
                    Path.Combine(
                        directory,
                        "profiles.json"));
            var activeStore =
                new JsonActiveScraperProfileStore(
                    Path.Combine(
                        directory,
                        "active.json"));
            var customProfile = new ScraperProfile(
                "My German dictionary",
                "https://dictionary.test/{word}",
                "de",
                "en",
                [
                    new ScraperExtractionRule(
                        DictionaryField.Translation,
                        ".translation")
                ]);

            await customStore.SaveAsync(
                [customProfile],
                TestContext.Current.CancellationToken);
            await activeStore.SetActiveProfileNameAsync(
                "de",
                "en",
                customProfile.Name,
                TestContext.Current.CancellationToken);

            var resolver = new ScraperProfileResolver(
                customStore,
                activeStore,
                BuiltInScraperProfiles.All);

            var resolved = await resolver.ResolveAsync(
                "de",
                "en",
                TestContext.Current.CancellationToken);

            Assert.NotNull(resolved);
            Assert.Equal(customProfile.Name, resolved.Name);
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
