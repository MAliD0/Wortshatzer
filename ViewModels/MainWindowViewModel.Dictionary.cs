using CommunityToolkit.Mvvm.ComponentModel;
using Wortshatzer.Core.Dictionary;
using Wortshatzer.Core.Words;

namespace Wortshatzer.ViewModels;

public partial class MainWindowViewModel
{
    private IDictionaryLookupService? _dictionaryLookupService;
    private IScraperProfileResolver? _scraperProfileResolver;
    private CancellationTokenSource? _dictionaryLookupCancellation;
    private bool _isDictionaryConfigured;

    [ObservableProperty]
    private string _dictionaryDetailsText = string.Empty;

    [ObservableProperty]
    private string _dictionaryStatus =
        "Dictionary details have not been loaded.";

    [ObservableProperty]
    private bool _hasDictionaryDetails;

    [ObservableProperty]
    private bool _isDictionaryLoading;

    public event Action<DictionaryLookupResult>?
        DictionaryResultReady;

    public event Action<WordTranslation>? TranslationCompleted;

    public void ConfigureDictionaryIntegration(
        IDictionaryLookupService dictionaryLookupService,
        IScraperProfileResolver scraperProfileResolver)
    {
        ArgumentNullException.ThrowIfNull(dictionaryLookupService);
        ArgumentNullException.ThrowIfNull(scraperProfileResolver);

        if (_isDictionaryConfigured)
        {
            throw new InvalidOperationException(
                "Dictionary integration is already configured.");
        }

        _dictionaryLookupService = dictionaryLookupService;
        _scraperProfileResolver = scraperProfileResolver;
        TranslationCompleted += OnTranslationCompleted;
        _isDictionaryConfigured = true;
    }

    public void DisposeDictionaryIntegration()
    {
        if (!_isDictionaryConfigured)
        {
            return;
        }

        TranslationCompleted -= OnTranslationCompleted;
        _dictionaryLookupCancellation?.Cancel();
        _dictionaryLookupCancellation?.Dispose();
        _dictionaryLookupCancellation = null;
        _isDictionaryConfigured = false;
    }

    public void ResetDictionaryDetails()
    {
        _dictionaryLookupCancellation?.Cancel();
        HasDictionaryDetails = false;
        IsDictionaryLoading = false;
        DictionaryDetailsText = string.Empty;
        DictionaryStatus =
            "Translate a word to load dictionary details.";
    }

    private void OnTranslationCompleted(
        WordTranslation translation)
    {
        _dictionaryLookupCancellation?.Cancel();
        _dictionaryLookupCancellation?.Dispose();
        _dictionaryLookupCancellation =
            new CancellationTokenSource();

        _ = LoadDictionaryDetailsAsync(
            translation,
            _dictionaryLookupCancellation.Token);
    }

    private async Task LoadDictionaryDetailsAsync(
        WordTranslation translation,
        CancellationToken cancellationToken)
    {
        var lookupService = _dictionaryLookupService;
        var profileResolver = _scraperProfileResolver;

        if (lookupService is null || profileResolver is null)
        {
            return;
        }

        HasDictionaryDetails = false;
        DictionaryDetailsText = string.Empty;
        IsDictionaryLoading = true;
        DictionaryStatus = "Finding active dictionary profile…";

        try
        {
            var languagePair =
                translation.CapturedWord.LanguagePair;
            var profile = await profileResolver.ResolveAsync(
                languagePair.Source.Code,
                languagePair.Target.Code,
                cancellationToken);

            if (profile is null)
            {
                DictionaryStatus =
                    $"No dictionary profile is configured for {languagePair.Source.DisplayName} → {languagePair.Target.DisplayName}.";
                return;
            }

            DictionaryStatus =
                $"Loading details from {profile.Name}…";

            var result = await lookupService.LookupAsync(
                profile,
                translation.CapturedWord.Text,
                cancellationToken);
            var formatted = DictionaryResultFormatter.Format(
                result);

            if (string.IsNullOrWhiteSpace(formatted))
            {
                DictionaryStatus =
                    $"{profile.Name} returned no configured fields.";
                return;
            }

            DictionaryDetailsText = formatted;
            HasDictionaryDetails = true;
            DictionaryStatus = $"Dictionary: {profile.Name}";
            DictionaryResultReady?.Invoke(result);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            HasDictionaryDetails = false;
            DictionaryStatus =
                $"Dictionary details unavailable: {exception.Message}";
        }
        finally
        {
            IsDictionaryLoading = false;
        }
    }
}
