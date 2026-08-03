namespace Wortshatzer.Core.Dictionary;

public interface IDictionaryLookupService
{
    Task<DictionaryLookupResult> LookupAsync(
        ScraperProfile profile,
        string word,
        CancellationToken cancellationToken = default);
}
