using System.Text.Json;
using System.Text.Json.Serialization;
using Wortshatzer.Core.Dictionary;

namespace Wortshatzer.Infrastructure.Dictionary;

public sealed class JsonScraperProfileStore :
    IScraperProfileStore
{
    private const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Converters =
            {
                new JsonStringEnumConverter(
                    JsonNamingPolicy.CamelCase)
            }
        };

    private readonly string _filePath;

    public string FilePath => _filePath;

    public JsonScraperProfileStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = Path.GetFullPath(filePath);
    }

    public async Task<IReadOnlyList<ScraperProfile>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        try
        {
            await using var stream = new FileStream(
                _filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                useAsync: true);

            var document =
                await JsonSerializer.DeserializeAsync<ProfileFile>(
                    stream,
                    SerializerOptions,
                    cancellationToken)
                ?? throw new InvalidDataException(
                    "The profile file is empty.");

            if (document.SchemaVersion != CurrentSchemaVersion)
            {
                throw new InvalidDataException(
                    $"Unsupported scraper profile schema version {document.SchemaVersion}.");
            }

            return document.Profiles
                .Select(ToDomain)
                .ToArray();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new ScraperProfilePersistenceException(
                $"Could not load scraper profiles from '{_filePath}'.",
                exception);
        }
    }

    public async Task SaveAsync(
        IReadOnlyCollection<ScraperProfile> profiles,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new ScraperProfilePersistenceException(
                "The scraper profile path has no directory.");
        var temporaryPath =
            _filePath + $".{Guid.NewGuid():N}.tmp";

        try
        {
            Directory.CreateDirectory(directory);

            var document = new ProfileFile
            {
                SchemaVersion = CurrentSchemaVersion,
                Profiles = profiles
                    .OrderBy(profile => profile.Name)
                    .Select(ToDocument)
                    .ToList()
            };

            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    document,
                    SerializerOptions,
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(
                temporaryPath,
                _filePath,
                overwrite: true);
        }
        catch (OperationCanceledException)
        {
            TryDelete(temporaryPath);
            throw;
        }
        catch (Exception exception)
        {
            TryDelete(temporaryPath);
            throw new ScraperProfilePersistenceException(
                $"Could not save scraper profiles to '{_filePath}'.",
                exception);
        }
    }

    private static ProfileDocument ToDocument(
        ScraperProfile profile)
    {
        return new ProfileDocument
        {
            Name = profile.Name,
            SearchUrlTemplate = profile.SearchUrlTemplate,
            SourceLanguageCode = profile.SourceLanguageCode,
            TargetLanguageCode = profile.TargetLanguageCode,
            EntrySelector = profile.EntrySelector,
            Suggestion = profile.SuggestionRule is null
                ? null
                : new SuggestionDocument
                {
                    Selector =
                        profile.SuggestionRule.Selector,
                    FallbackSelectors =
                        profile.SuggestionRule
                            .FallbackSelectors.ToList()
                },
            Fields = profile.Fields
                .Select(rule => new RuleDocument
                {
                    Field = rule.Field,
                    CustomFieldName =
                        rule.Field == DictionaryField.Custom
                            ? rule.OutputName
                            : null,
                    Selector = rule.Selector,
                    FallbackSelectors =
                        rule.FallbackSelectors.ToList(),
                    ValueSource = rule.ValueSource,
                    AttributeName = rule.AttributeName,
                    ResultMode = rule.ResultMode,
                    IsRequired = rule.IsRequired,
                    RemoveDuplicates = rule.RemoveDuplicates,
                    MaximumResults = rule.MaximumResults
                })
                .ToList()
        };
    }

    private static ScraperProfile ToDomain(
        ProfileDocument profile)
    {
        return new ScraperProfile(
            profile.Name,
            profile.SearchUrlTemplate,
            profile.SourceLanguageCode,
            profile.TargetLanguageCode,
            profile.Fields.Select(rule =>
                new ScraperExtractionRule(
                    rule.Field,
                    rule.Selector,
                    rule.ValueSource,
                    rule.ResultMode,
                    rule.IsRequired,
                    rule.RemoveDuplicates,
                    rule.MaximumResults,
                    rule.AttributeName,
                    rule.CustomFieldName,
                    rule.FallbackSelectors)),
            profile.EntrySelector,
            profile.Suggestion is null
                ? null
                : new ScraperSuggestionRule(
                    profile.Suggestion.Selector,
                    profile.Suggestion.FallbackSelectors));
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // A stale temporary file is harmless.
        }
    }

    private sealed class ProfileFile
    {
        public int SchemaVersion { get; set; }

        public List<ProfileDocument> Profiles { get; set; } = [];
    }

    private sealed class ProfileDocument
    {
        public string Name { get; set; } = string.Empty;

        public string SearchUrlTemplate { get; set; } =
            string.Empty;

        public string SourceLanguageCode { get; set; } =
            string.Empty;

        public string TargetLanguageCode { get; set; } =
            string.Empty;

        public string? EntrySelector { get; set; }

        public SuggestionDocument? Suggestion { get; set; }

        public List<RuleDocument> Fields { get; set; } = [];
    }

    private sealed class SuggestionDocument
    {
        public string Selector { get; set; } = string.Empty;

        public List<string> FallbackSelectors { get; set; } = [];
    }

    private sealed class RuleDocument
    {
        public DictionaryField Field { get; set; }

        public string? CustomFieldName { get; set; }

        public string Selector { get; set; } = string.Empty;

        public List<string> FallbackSelectors { get; set; } = [];

        public ScraperValueSource ValueSource { get; set; }

        public string? AttributeName { get; set; }

        public ScraperResultMode ResultMode { get; set; }

        public bool IsRequired { get; set; }

        public bool RemoveDuplicates { get; set; } = true;

        public int MaximumResults { get; set; } = 20;
    }
}
