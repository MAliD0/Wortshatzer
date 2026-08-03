using Wortshatzer.Core.Dictionary;

namespace Wortshatzer.Infrastructure.Dictionary;

public sealed class ScraperProfileResolver :
    IScraperProfileResolver
{
    private readonly IScraperProfileStore _customProfileStore;
    private readonly IActiveScraperProfileStore _activeProfileStore;
    private readonly IReadOnlyList<ScraperProfile> _builtInProfiles;

    public ScraperProfileResolver(
        IScraperProfileStore customProfileStore,
        IActiveScraperProfileStore activeProfileStore,
        IReadOnlyList<ScraperProfile> builtInProfiles)
    {
        ArgumentNullException.ThrowIfNull(customProfileStore);
        ArgumentNullException.ThrowIfNull(activeProfileStore);
        ArgumentNullException.ThrowIfNull(builtInProfiles);

        _customProfileStore = customProfileStore;
        _activeProfileStore = activeProfileStore;
        _builtInProfiles = builtInProfiles;
    }

    public async Task<ScraperProfile?> ResolveAsync(
        string sourceLanguageCode,
        string targetLanguageCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            sourceLanguageCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            targetLanguageCode);

        var source =
            sourceLanguageCode.Trim().ToLowerInvariant();
        var target =
            targetLanguageCode.Trim().ToLowerInvariant();
        var customProfiles =
            await _customProfileStore.LoadAsync(
                cancellationToken);
        var availableProfiles = _builtInProfiles
            .Concat(customProfiles)
            .Where(profile =>
                profile.SourceLanguageCode == source
                && profile.TargetLanguageCode == target)
            .ToArray();

        if (availableProfiles.Length == 0)
        {
            return null;
        }

        var activeName =
            await _activeProfileStore
                .GetActiveProfileNameAsync(
                    source,
                    target,
                    cancellationToken);

        if (!string.IsNullOrWhiteSpace(activeName))
        {
            var selected = availableProfiles.FirstOrDefault(
                profile => string.Equals(
                    profile.Name,
                    activeName,
                    StringComparison.OrdinalIgnoreCase));

            if (selected is not null)
            {
                return selected;
            }
        }

        return availableProfiles[0];
    }
}
