namespace Wortshatzer.Core.Dictionary;

public interface IActiveScraperProfileStore
{
    Task<string?> GetActiveProfileNameAsync(
        string sourceLanguageCode,
        string targetLanguageCode,
        CancellationToken cancellationToken = default);

    Task SetActiveProfileNameAsync(
        string sourceLanguageCode,
        string targetLanguageCode,
        string profileName,
        CancellationToken cancellationToken = default);
}

public interface IScraperProfileResolver
{
    Task<ScraperProfile?> ResolveAsync(
        string sourceLanguageCode,
        string targetLanguageCode,
        CancellationToken cancellationToken = default);
}
