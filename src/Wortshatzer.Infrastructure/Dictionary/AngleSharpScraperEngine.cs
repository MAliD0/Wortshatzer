using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Wortshatzer.Core.Dictionary;

namespace Wortshatzer.Infrastructure.Dictionary;

public sealed class AngleSharpScraperEngine :
    IDictionaryScraperEngine,
    IDictionarySuggestionExtractor
{
    private readonly HtmlParser _parser = new();

    public async Task<DictionaryLookupResult> ExtractAsync(
        ScraperProfile profile,
        string word,
        string html,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(word);
        ArgumentException.ThrowIfNullOrWhiteSpace(html);

        cancellationToken.ThrowIfCancellationRequested();

        IDocument document;

        try
        {
            document = await _parser.ParseDocumentAsync(
                html,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new DictionaryScrapingException(
                "The downloaded HTML could not be parsed.",
                exception);
        }

        var sourceUri = profile.BuildSearchUri(word);
        var scope = ResolveEntryScope(
            document,
            profile.EntrySelector);

        var extractedFields =
            new Dictionary<string, List<string>>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var rule in profile.Fields)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var values = ExtractRule(
                scope,
                rule,
                sourceUri);

            if (values.Count == 0 && rule.IsRequired)
            {
                throw new DictionaryScrapingException(
                    $"Required field '{rule.OutputName}' was not found by profile '{profile.Name}'.");
            }

            if (values.Count == 0)
            {
                continue;
            }

            if (!extractedFields.TryGetValue(
                    rule.OutputName,
                    out var existingValues))
            {
                existingValues = [];
                extractedFields.Add(
                    rule.OutputName,
                    existingValues);
            }

            existingValues.AddRange(values);

            if (rule.RemoveDuplicates)
            {
                var distinct = existingValues
                    .Distinct(StringComparer.Ordinal)
                    .Take(rule.MaximumResults)
                    .ToArray();

                existingValues.Clear();
                existingValues.AddRange(distinct);
            }
        }

        var readOnlyFields = extractedFields.ToDictionary(
            item => item.Key,
            item => (IReadOnlyList<string>)item.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);

        return new DictionaryLookupResult(
            word,
            profile.Name,
            sourceUri,
            readOnlyFields);
    }

    public async Task<DictionarySuggestion?>
        ExtractClosestSuggestionAsync(
            ScraperProfile profile,
            string query,
            string html,
            Uri pageUri,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(html);
        ArgumentNullException.ThrowIfNull(pageUri);

        var rule = profile.SuggestionRule;

        if (rule is null)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        IDocument document;

        try
        {
            document = await _parser.ParseDocumentAsync(
                html,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new DictionaryScrapingException(
                "The suggestion page could not be parsed.",
                exception);
        }

        var suggestions = new List<DictionarySuggestion>();

        foreach (var selector in rule.EnumerateSelectors())
        {
            IHtmlCollection<IElement> candidates;

            try
            {
                candidates =
                    document.QuerySelectorAll(selector);
            }
            catch (Exception exception)
            {
                throw new DictionaryScrapingException(
                    $"Suggestion selector '{selector}' is invalid.",
                    exception);
            }

            foreach (var candidate in candidates)
            {
                var href = candidate.GetAttribute("href");

                if (string.IsNullOrWhiteSpace(href)
                    || !Uri.TryCreate(
                        pageUri,
                        href,
                        out var suggestionUri)
                    || suggestionUri.Scheme
                        != Uri.UriSchemeHttps
                    || !string.Equals(
                        suggestionUri.Host,
                        pageUri.Host,
                        StringComparison.OrdinalIgnoreCase)
                    || IsDictionaryRoot(
                        profile,
                        suggestionUri))
                {
                    continue;
                }

                var word = Uri.UnescapeDataString(
                    suggestionUri.Segments[^1])
                    .Trim('/');

                if (string.IsNullOrWhiteSpace(word))
                {
                    word =
                        CollapseWhitespace(candidate.TextContent);
                }

                if (string.IsNullOrWhiteSpace(word)
                    || suggestions.Any(item =>
                        item.SourceUri == suggestionUri))
                {
                    continue;
                }

                suggestions.Add(
                    new DictionarySuggestion(
                        word,
                        suggestionUri));
            }

        }

        return suggestions
            .OrderBy(item => CalculateEditDistance(
                query,
                item.Word))
            .ThenBy(item => item.Word.Length)
            .FirstOrDefault();
    }

    private static bool IsDictionaryRoot(
        ScraperProfile profile,
        Uri candidateUri)
    {
        var rootUrl = profile.SearchUrlTemplate.Replace(
            "{word}",
            string.Empty,
            StringComparison.Ordinal);

        if (!Uri.TryCreate(
                rootUrl,
                UriKind.Absolute,
                out var rootUri))
        {
            return false;
        }

        return string.Equals(
                candidateUri.Host,
                rootUri.Host,
                StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                candidateUri.AbsolutePath.TrimEnd('/'),
                rootUri.AbsolutePath.TrimEnd('/'),
                StringComparison.OrdinalIgnoreCase);
    }

    private static int CalculateEditDistance(
        string first,
        string second)
    {
        var source = first.Trim().ToLowerInvariant();
        var target = second.Trim().ToLowerInvariant();
        var previous = Enumerable
            .Range(0, target.Length + 1)
            .ToArray();
        var current = new int[target.Length + 1];

        for (var sourceIndex = 1;
             sourceIndex <= source.Length;
             sourceIndex++)
        {
            current[0] = sourceIndex;

            for (var targetIndex = 1;
                 targetIndex <= target.Length;
                 targetIndex++)
            {
                var substitutionCost =
                    source[sourceIndex - 1]
                        == target[targetIndex - 1]
                        ? 0
                        : 1;

                current[targetIndex] = Math.Min(
                    Math.Min(
                        current[targetIndex - 1] + 1,
                        previous[targetIndex] + 1),
                    previous[targetIndex - 1]
                        + substitutionCost);
            }

            (previous, current) = (current, previous);
        }

        return previous[target.Length];
    }

    private static IElement ResolveEntryScope(
        IDocument document,
        string? entrySelector)
    {
        var documentRoot = document.DocumentElement
            ?? throw new DictionaryScrapingException(
                "The downloaded page has no document element.");

        if (entrySelector is null)
        {
            return documentRoot;
        }

        try
        {
            return document.QuerySelector(entrySelector)
                ?? throw new DictionaryScrapingException(
                    $"Entry selector '{entrySelector}' did not match the page.");
        }
        catch (DictionaryScrapingException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new DictionaryScrapingException(
                $"Entry selector '{entrySelector}' is invalid.",
                exception);
        }
    }

    private static IReadOnlyList<string> ExtractRule(
        IElement scope,
        ScraperExtractionRule rule,
        Uri sourceUri)
    {
        IHtmlCollection<IElement>? matchedElements = null;

        foreach (var selector in rule.EnumerateSelectors())
        {
            try
            {
                var candidates = scope.QuerySelectorAll(selector);

                if (candidates.Length > 0)
                {
                    matchedElements = candidates;
                    break;
                }
            }
            catch (Exception exception)
            {
                throw new DictionaryScrapingException(
                    $"Selector '{selector}' for field '{rule.OutputName}' is invalid.",
                    exception);
            }
        }

        if (matchedElements is null)
        {
            return [];
        }

        var maximumResults = rule.ResultMode == ScraperResultMode.First
            ? 1
            : rule.MaximumResults;
        var results = new List<string>();

        foreach (var element in matchedElements.Take(maximumResults))
        {
            var value = ReadValue(
                element,
                rule,
                sourceUri);

            if (!string.IsNullOrWhiteSpace(value))
            {
                results.Add(value);
            }
        }

        if (rule.RemoveDuplicates)
        {
            return results
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        return results;
    }

    private static string? ReadValue(
        IElement element,
        ScraperExtractionRule rule,
        Uri sourceUri)
    {
        var value = rule.ValueSource switch
        {
            ScraperValueSource.Text =>
                CollapseWhitespace(element.TextContent),
            ScraperValueSource.Html =>
                element.InnerHtml.Trim(),
            ScraperValueSource.Attribute =>
                element.GetAttribute(rule.AttributeName!),
            _ => null
        };

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();

        if (rule.ValueSource == ScraperValueSource.Attribute
            && Uri.TryCreate(
                sourceUri,
                value,
                out var absoluteUri))
        {
            return absoluteUri.ToString();
        }

        return value;
    }

    private static string CollapseWhitespace(string value)
    {
        return string.Join(
            ' ',
            value.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries
                    | StringSplitOptions.TrimEntries));
    }
}
