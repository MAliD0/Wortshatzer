using System.Collections.Concurrent;
using System.Text;
using Wortshatzer.Core.Dictionary;

namespace Wortshatzer.Infrastructure.Dictionary;

public sealed class HttpDictionaryLookupService :
    IDictionaryLookupService
{
    private const int DefaultMaximumResponseBytes =
        2 * 1024 * 1024;

    private readonly HttpClient _httpClient;
    private readonly IDictionaryScraperEngine _scraperEngine;
    private readonly IDictionarySuggestionExtractor?
        _suggestionExtractor;
    private readonly TimeSpan _cacheDuration;
    private readonly int _maximumResponseBytes;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache =
        new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _fetchLock = new(1, 1);

    public HttpDictionaryLookupService(
        HttpClient httpClient,
        IDictionaryScraperEngine scraperEngine,
        TimeSpan? cacheDuration = null,
        int maximumResponseBytes = DefaultMaximumResponseBytes,
        Func<DateTimeOffset>? utcNow = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(scraperEngine);

        var configuredDuration =
            cacheDuration ?? TimeSpan.FromHours(12);

        if (configuredDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cacheDuration),
                "The cache duration must be positive.");
        }

        if (maximumResponseBytes is < 1024
            or > 10 * 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumResponseBytes),
                "Dictionary responses must be limited to 1 KB–10 MB.");
        }

        _httpClient = httpClient;
        _scraperEngine = scraperEngine;
        _suggestionExtractor =
            scraperEngine as IDictionarySuggestionExtractor;
        _cacheDuration = configuredDuration;
        _maximumResponseBytes = maximumResponseBytes;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<DictionaryLookupResult> LookupAsync(
        ScraperProfile profile,
        string word,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(word);

        var normalizedWord = word.Trim();
        var sourceUri = profile.BuildSearchUri(normalizedWord);
        var cacheKey = BuildCacheKey(
            profile,
            sourceUri);

        if (TryGetCached(cacheKey, out var cached))
        {
            return cached;
        }

        await _fetchLock.WaitAsync(cancellationToken);

        try
        {
            if (TryGetCached(cacheKey, out cached))
            {
                return cached;
            }

            var page = await FetchPageAsync(
                profile,
                sourceUri,
                cancellationToken);
            DictionaryLookupResult result;

            try
            {
                result = await _scraperEngine.ExtractAsync(
                    profile,
                    normalizedWord,
                    page.Html,
                    cancellationToken);
            }
            catch (DictionaryScrapingException)
            {
                var recovered =
                    await TryClosestSuggestionAsync(
                        profile,
                        normalizedWord,
                        page,
                        cancellationToken);

                if (recovered is null)
                {
                    throw;
                }

                result = recovered;
            }

            _cache[cacheKey] = new CacheEntry(
                result,
                _utcNow().Add(_cacheDuration));

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DictionaryScrapingException)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            throw new DictionaryScrapingException(
                $"Could not reach dictionary '{profile.Name}'.",
                exception);
        }
        catch (Exception exception)
        {
            throw new DictionaryScrapingException(
                $"Dictionary lookup with '{profile.Name}' failed.",
                exception);
        }
        finally
        {
            _fetchLock.Release();
        }
    }

    public void ClearCache()
    {
        _cache.Clear();
    }

    private async Task<DictionaryLookupResult?>
        TryClosestSuggestionAsync(
            ScraperProfile profile,
            string originalWord,
            FetchedPage suggestionPage,
            CancellationToken cancellationToken)
    {
        if (profile.SuggestionRule is null
            || _suggestionExtractor is null)
        {
            return null;
        }

        var suggestion =
            await _suggestionExtractor
                .ExtractFirstSuggestionAsync(
                    profile,
                    suggestionPage.Html,
                    suggestionPage.SourceUri,
                    cancellationToken);

        if (suggestion is null
            || suggestion.SourceUri
                == suggestionPage.SourceUri)
        {
            return null;
        }

        var entryPage = await FetchPageAsync(
            profile,
            suggestion.SourceUri,
            cancellationToken);
        var extracted =
            await _scraperEngine.ExtractAsync(
                profile,
                suggestion.Word,
                entryPage.Html,
                cancellationToken);

        return new DictionaryLookupResult(
            originalWord,
            extracted.SourceName,
            entryPage.SourceUri,
            extracted.Fields);
    }

    private async Task<FetchedPage> FetchPageAsync(
        ScraperProfile profile,
        Uri sourceUri,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            sourceUri);
        request.Headers.Accept.ParseAdd(
            "text/html,application/xhtml+xml");

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new DictionaryScrapingException(
                $"Dictionary '{profile.Name}' returned HTTP {(int)response.StatusCode} ({response.StatusCode}).");
        }

        var contentLength =
            response.Content.Headers.ContentLength;

        if (contentLength.HasValue
            && contentLength.Value > _maximumResponseBytes)
        {
            throw ResponseTooLarge(profile);
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(
            cancellationToken);

        if (bytes.Length > _maximumResponseBytes)
        {
            throw ResponseTooLarge(profile);
        }

        return new FetchedPage(
            response.RequestMessage?.RequestUri
                ?? sourceUri,
            DecodeHtml(response, bytes));
    }

    private static string BuildCacheKey(
        ScraperProfile profile,
        Uri sourceUri)
    {
        var key = new StringBuilder();
        key.AppendLine(profile.Name);
        key.AppendLine(sourceUri.AbsoluteUri);
        key.AppendLine(profile.EntrySelector ?? string.Empty);
        key.AppendLine(
            profile.SuggestionRule?.Selector
                ?? string.Empty);
        key.AppendLine(string.Join(
            "\u001F",
            profile.SuggestionRule?.FallbackSelectors
                ?? []));

        foreach (var rule in profile.Fields)
        {
            key.Append((int)rule.Field);
            key.Append('|');
            key.Append(rule.OutputName);
            key.Append('|');
            key.Append(rule.Selector);
            key.Append('|');
            key.Append(string.Join("\u001F", rule.FallbackSelectors));
            key.Append('|');
            key.Append((int)rule.ValueSource);
            key.Append('|');
            key.Append(rule.AttributeName);
            key.Append('|');
            key.Append((int)rule.ResultMode);
            key.Append('|');
            key.Append(rule.IsRequired);
            key.Append('|');
            key.Append(rule.RemoveDuplicates);
            key.Append('|');
            key.AppendLine(rule.MaximumResults.ToString());
        }

        return key.ToString();
    }

    private bool TryGetCached(
        string cacheKey,
        out DictionaryLookupResult result)
    {
        if (_cache.TryGetValue(cacheKey, out var entry))
        {
            if (entry.ExpiresAt > _utcNow())
            {
                result = entry.Result;
                return true;
            }

            _cache.TryRemove(cacheKey, out _);
        }

        result = null!;
        return false;
    }

    private static string DecodeHtml(
        HttpResponseMessage response,
        byte[] bytes)
    {
        var charset = response.Content.Headers.ContentType?.CharSet;

        if (!string.IsNullOrWhiteSpace(charset))
        {
            try
            {
                return Encoding.GetEncoding(
                        charset.Trim('"'))
                    .GetString(bytes);
            }
            catch (ArgumentException)
            {
                // Invalid server-provided charset; UTF-8 is the safe web default.
            }
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static DictionaryScrapingException ResponseTooLarge(
        ScraperProfile profile)
    {
        return new DictionaryScrapingException(
            $"Dictionary '{profile.Name}' returned a page larger than the configured safety limit.");
    }

    private sealed record FetchedPage(
        Uri SourceUri,
        string Html);

    private sealed record CacheEntry(
        DictionaryLookupResult Result,
        DateTimeOffset ExpiresAt);
}
