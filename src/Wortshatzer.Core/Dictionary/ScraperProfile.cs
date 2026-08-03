namespace Wortshatzer.Core.Dictionary;

public enum DictionaryField
{
    Headword,
    Translation,
    Definition,
    PartOfSpeech,
    Article,
    Gender,
    Plural,
    Conjugation,
    Example,
    ExampleTranslation,
    Pronunciation,
    AudioUrl,
    Custom
}

public enum ScraperValueSource
{
    Text,
    Html,
    Attribute
}

public enum ScraperResultMode
{
    First,
    All
}

public sealed record ScraperExtractionRule
{
    public DictionaryField Field { get; }

    public string OutputName { get; }

    public string Selector { get; }

    public IReadOnlyList<string> FallbackSelectors { get; }

    public ScraperValueSource ValueSource { get; }

    public string? AttributeName { get; }

    public ScraperResultMode ResultMode { get; }

    public bool IsRequired { get; }

    public bool RemoveDuplicates { get; }

    public int MaximumResults { get; }

    public ScraperExtractionRule(
        DictionaryField field,
        string selector,
        ScraperValueSource valueSource = ScraperValueSource.Text,
        ScraperResultMode resultMode = ScraperResultMode.All,
        bool isRequired = false,
        bool removeDuplicates = true,
        int maximumResults = 20,
        string? attributeName = null,
        string? customFieldName = null,
        IEnumerable<string>? fallbackSelectors = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);

        if (!Enum.IsDefined(field))
        {
            throw new ArgumentOutOfRangeException(nameof(field));
        }

        if (!Enum.IsDefined(valueSource))
        {
            throw new ArgumentOutOfRangeException(nameof(valueSource));
        }

        if (!Enum.IsDefined(resultMode))
        {
            throw new ArgumentOutOfRangeException(nameof(resultMode));
        }

        if (maximumResults is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumResults),
                "A field can extract between 1 and 100 results.");
        }

        if (valueSource == ScraperValueSource.Attribute
            && string.IsNullOrWhiteSpace(attributeName))
        {
            throw new ArgumentException(
                "Attribute extraction requires an attribute name.",
                nameof(attributeName));
        }

        if (field == DictionaryField.Custom
            && string.IsNullOrWhiteSpace(customFieldName))
        {
            throw new ArgumentException(
                "A custom field requires a name.",
                nameof(customFieldName));
        }

        var fallbacks = (fallbackSelectors ?? [])
            .Select(value => value?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Field = field;
        OutputName = field == DictionaryField.Custom
            ? customFieldName!.Trim()
            : field.ToString();
        Selector = selector.Trim();
        FallbackSelectors = fallbacks;
        ValueSource = valueSource;
        AttributeName = attributeName?.Trim();
        ResultMode = resultMode;
        IsRequired = isRequired;
        RemoveDuplicates = removeDuplicates;
        MaximumResults = maximumResults;
    }

    public IEnumerable<string> EnumerateSelectors()
    {
        yield return Selector;

        foreach (var fallbackSelector in FallbackSelectors)
        {
            yield return fallbackSelector;
        }
    }
}

public sealed record ScraperSuggestionRule
{
    public string Selector { get; }

    public IReadOnlyList<string> FallbackSelectors { get; }

    public string? SearchUrlTemplate { get; }

    public ScraperSuggestionRule(
        string selector,
        IEnumerable<string>? fallbackSelectors = null,
        string? searchUrlTemplate = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);

        if (!string.IsNullOrWhiteSpace(searchUrlTemplate))
        {
            if (!searchUrlTemplate.Contains(
                    "{word}",
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "The suggestion URL must contain the {word} placeholder.",
                    nameof(searchUrlTemplate));
            }

            var validationUrl = searchUrlTemplate.Replace(
                "{word}",
                "test",
                StringComparison.Ordinal);

            if (!Uri.TryCreate(
                    validationUrl,
                    UriKind.Absolute,
                    out var uri)
                || uri.Scheme != Uri.UriSchemeHttps)
            {
                throw new ArgumentException(
                    "The suggestion URL must be an absolute HTTPS address.",
                    nameof(searchUrlTemplate));
            }
        }

        Selector = selector.Trim();
        SearchUrlTemplate =
            string.IsNullOrWhiteSpace(searchUrlTemplate)
                ? null
                : searchUrlTemplate.Trim();
        FallbackSelectors = (fallbackSelectors ?? [])
            .Select(value => value?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public Uri? BuildSearchUri(string word)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(word);

        return SearchUrlTemplate is null
            ? null
            : new Uri(
                SearchUrlTemplate.Replace(
                    "{word}",
                    Uri.EscapeDataString(word.Trim()),
                    StringComparison.Ordinal));
    }

    public IEnumerable<string> EnumerateSelectors()
    {
        yield return Selector;

        foreach (var fallbackSelector in FallbackSelectors)
        {
            yield return fallbackSelector;
        }
    }
}

public sealed record DictionarySuggestion(
    string Word,
    Uri SourceUri);

public interface IDictionarySuggestionExtractor
{
    Task<DictionarySuggestion?> ExtractClosestSuggestionAsync(
        ScraperProfile profile,
        string query,
        string html,
        Uri pageUri,
        CancellationToken cancellationToken = default);
}

public sealed record ScraperProfile
{
    public string Name { get; }

    public string SearchUrlTemplate { get; }

    public string SourceLanguageCode { get; }

    public string TargetLanguageCode { get; }

    public string? EntrySelector { get; }

    public ScraperSuggestionRule? SuggestionRule { get; }

    public IReadOnlyList<ScraperExtractionRule> Fields { get; }

    public ScraperProfile(
        string name,
        string searchUrlTemplate,
        string sourceLanguageCode,
        string targetLanguageCode,
        IEnumerable<ScraperExtractionRule> fields,
        string? entrySelector = null,
        ScraperSuggestionRule? suggestionRule = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(searchUrlTemplate);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLanguageCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetLanguageCode);
        ArgumentNullException.ThrowIfNull(fields);

        if (!searchUrlTemplate.Contains(
                "{word}",
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The search URL must contain the {word} placeholder.",
                nameof(searchUrlTemplate));
        }

        var validationUrl = searchUrlTemplate.Replace(
            "{word}",
            "test",
            StringComparison.Ordinal);

        if (!Uri.TryCreate(
                validationUrl,
                UriKind.Absolute,
                out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException(
                "The search URL must be an absolute HTTPS address.",
                nameof(searchUrlTemplate));
        }

        var fieldArray = fields.ToArray();

        if (fieldArray.Length == 0)
        {
            throw new ArgumentException(
                "A scraper profile must contain at least one field.",
                nameof(fields));
        }

        Name = name.Trim();
        SearchUrlTemplate = searchUrlTemplate.Trim();
        SourceLanguageCode =
            sourceLanguageCode.Trim().ToLowerInvariant();
        TargetLanguageCode =
            targetLanguageCode.Trim().ToLowerInvariant();
        EntrySelector = string.IsNullOrWhiteSpace(entrySelector)
            ? null
            : entrySelector.Trim();
        SuggestionRule = suggestionRule;
        Fields = fieldArray;
    }

    public Uri BuildSearchUri(string word)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(word);

        return new Uri(
            SearchUrlTemplate.Replace(
                "{word}",
                Uri.EscapeDataString(word.Trim()),
                StringComparison.Ordinal));
    }
}

public sealed record DictionaryLookupResult
{
    public string Query { get; }

    public string SourceName { get; }

    public Uri SourceUri { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Fields { get; }

    public DictionaryLookupResult(
        string query,
        string sourceName,
        Uri sourceUri,
        IReadOnlyDictionary<string, IReadOnlyList<string>> fields)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        ArgumentNullException.ThrowIfNull(sourceUri);
        ArgumentNullException.ThrowIfNull(fields);

        Query = query.Trim();
        SourceName = sourceName.Trim();
        SourceUri = sourceUri;
        Fields = fields;
    }

    public IReadOnlyList<string> GetValues(
        DictionaryField field)
    {
        return Fields.TryGetValue(
                field.ToString(),
                out var values)
            ? values
            : [];
    }
}

public interface IDictionaryScraperEngine
{
    Task<DictionaryLookupResult> ExtractAsync(
        ScraperProfile profile,
        string word,
        string html,
        CancellationToken cancellationToken = default);
}

public sealed class DictionaryScrapingException : Exception
{
    public DictionaryScrapingException(string message)
        : base(message)
    {
    }

    public DictionaryScrapingException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}
