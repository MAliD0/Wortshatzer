using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wortshatzer.Core.Dictionary;

namespace Wortshatzer.ViewModels;

public sealed record ScraperProfileListItem(
    ScraperProfile Profile,
    bool IsBuiltIn)
{
    public string DisplayName => Profile.Name;

    public string Kind => IsBuiltIn
        ? "Built-in"
        : "Custom";
}

public partial class ScraperRuleEditorViewModel :
    ViewModelBase
{
    [ObservableProperty]
    private DictionaryField _field;

    [ObservableProperty]
    private string _customFieldName = string.Empty;

    [ObservableProperty]
    private string _selector = string.Empty;

    [ObservableProperty]
    private string _fallbackSelectorsText = string.Empty;

    [ObservableProperty]
    private ScraperValueSource _valueSource;

    [ObservableProperty]
    private string _attributeName = string.Empty;

    [ObservableProperty]
    private ScraperResultMode _resultMode;

    [ObservableProperty]
    private bool _isRequired;

    [ObservableProperty]
    private bool _removeDuplicates = true;

    [ObservableProperty]
    private int _maximumResults = 20;

    public IReadOnlyList<DictionaryField> AvailableFields { get; } =
        Enum.GetValues<DictionaryField>();

    public IReadOnlyList<ScraperValueSource>
        AvailableValueSources { get; } =
        Enum.GetValues<ScraperValueSource>();

    public IReadOnlyList<ScraperResultMode>
        AvailableResultModes { get; } =
        Enum.GetValues<ScraperResultMode>();

    public IRelayCommand RemoveCommand { get; }

    public ScraperRuleEditorViewModel(
        Action<ScraperRuleEditorViewModel> remove)
    {
        ArgumentNullException.ThrowIfNull(remove);
        RemoveCommand = new RelayCommand(() => remove(this));
    }

    public ScraperRuleEditorViewModel(
        ScraperExtractionRule rule,
        Action<ScraperRuleEditorViewModel> remove)
        : this(remove)
    {
        ArgumentNullException.ThrowIfNull(rule);

        Field = rule.Field;
        CustomFieldName =
            rule.Field == DictionaryField.Custom
                ? rule.OutputName
                : string.Empty;
        Selector = rule.Selector;
        FallbackSelectorsText = string.Join(
            Environment.NewLine,
            rule.FallbackSelectors);
        ValueSource = rule.ValueSource;
        AttributeName = rule.AttributeName ?? string.Empty;
        ResultMode = rule.ResultMode;
        IsRequired = rule.IsRequired;
        RemoveDuplicates = rule.RemoveDuplicates;
        MaximumResults = rule.MaximumResults;
    }

    public ScraperExtractionRule BuildRule()
    {
        var fallbackSelectors =
            FallbackSelectorsText
                .Split(
                    ['\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries
                        | StringSplitOptions.TrimEntries);

        return new ScraperExtractionRule(
            Field,
            Selector,
            ValueSource,
            ResultMode,
            IsRequired,
            RemoveDuplicates,
            MaximumResults,
            string.IsNullOrWhiteSpace(AttributeName)
                ? null
                : AttributeName,
            string.IsNullOrWhiteSpace(CustomFieldName)
                ? null
                : CustomFieldName,
            fallbackSelectors);
    }
}

public partial class ScraperSettingsViewModel :
    ViewModelBase
{
    private readonly IScraperProfileStore _profileStore;
    private readonly IDictionaryLookupService _lookupService;
    private readonly IReadOnlyList<ScraperProfile> _builtInProfiles;
    private readonly List<ScraperProfile> _customProfiles = [];

    [ObservableProperty]
    private ScraperProfileListItem? _selectedProfile;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _searchUrlTemplate = string.Empty;

    [ObservableProperty]
    private string _sourceLanguageCode = "de";

    [ObservableProperty]
    private string _targetLanguageCode = "en";

    [ObservableProperty]
    private string _entrySelector = string.Empty;

    [ObservableProperty]
    private bool _useClosestSuggestion;

    [ObservableProperty]
    private string _suggestionSelector = string.Empty;

    [ObservableProperty]
    private string _suggestionFallbackSelectorsText =
        string.Empty;

    [ObservableProperty]
    private string _testWord = "vielleicht";

    [ObservableProperty]
    private string _previewText =
        "Choose or create a profile, then test it.";

    [ObservableProperty]
    private string _statusMessage =
        "Loading scraper profiles…";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isSelectedBuiltIn;

    public ObservableCollection<ScraperProfileListItem> Profiles
    {
        get;
    } = [];

    public ObservableCollection<ScraperRuleEditorViewModel> Rules
    {
        get;
    } = [];

    public IRelayCommand NewProfileCommand { get; }

    public IRelayCommand CloneProfileCommand { get; }

    public IRelayCommand AddRuleCommand { get; }

    public IAsyncRelayCommand SaveProfileCommand { get; }

    public IAsyncRelayCommand DeleteProfileCommand { get; }

    public IAsyncRelayCommand TestProfileCommand { get; }

    public ScraperSettingsViewModel(
        IScraperProfileStore profileStore,
        IDictionaryLookupService lookupService,
        IReadOnlyList<ScraperProfile> builtInProfiles)
    {
        ArgumentNullException.ThrowIfNull(profileStore);
        ArgumentNullException.ThrowIfNull(lookupService);
        ArgumentNullException.ThrowIfNull(builtInProfiles);

        _profileStore = profileStore;
        _lookupService = lookupService;
        _builtInProfiles = builtInProfiles;

        NewProfileCommand = new RelayCommand(NewProfile);
        CloneProfileCommand = new RelayCommand(CloneProfile);
        AddRuleCommand = new RelayCommand(AddRule);
        SaveProfileCommand =
            new AsyncRelayCommand(SaveProfileAsync);
        DeleteProfileCommand =
            new AsyncRelayCommand(DeleteProfileAsync);
        TestProfileCommand =
            new AsyncRelayCommand(TestProfileAsync);
    }

    public async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        IsBusy = true;

        try
        {
            _customProfiles.Clear();
            _customProfiles.AddRange(
                await _profileStore.LoadAsync(
                    cancellationToken));

            RefreshProfiles();
            SelectedProfile = Profiles.FirstOrDefault();
            StatusMessage =
                $"Loaded {Profiles.Count} scraper profiles.";
        }
        catch (ScraperProfilePersistenceException exception)
        {
            RefreshProfiles();
            SelectedProfile = Profiles.FirstOrDefault();
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void NewProfile()
    {
        SelectedProfile = null;
        IsSelectedBuiltIn = false;
        Name = "My dictionary";
        SearchUrlTemplate =
            "https://example.com/dictionary/{word}";
        SourceLanguageCode = "de";
        TargetLanguageCode = "en";
        EntrySelector = string.Empty;
        UseClosestSuggestion = false;
        SuggestionSelector = string.Empty;
        SuggestionFallbackSelectorsText = string.Empty;
        Rules.Clear();
        AddRule();
        PreviewText =
            "Configure the profile and use Test profile.";
        StatusMessage = "New custom profile.";
    }

    private void CloneProfile()
    {
        var selected = SelectedProfile;

        if (selected is null)
        {
            StatusMessage =
                "Choose a profile to clone.";
            return;
        }

        LoadEditor(selected.Profile);
        SelectedProfile = null;
        IsSelectedBuiltIn = false;
        Name = CreateUniqueCustomName(
            selected.Profile.Name + " custom");
        StatusMessage =
            "Cloned profile. Change it and save as a custom profile.";
    }

    private void AddRule()
    {
        Rules.Add(new ScraperRuleEditorViewModel(
            RemoveRule)
        {
            Field = DictionaryField.Translation,
            Selector = ".translation",
            ValueSource = ScraperValueSource.Text,
            ResultMode = ScraperResultMode.All,
            RemoveDuplicates = true,
            MaximumResults = 20
        });
    }

    private void RemoveRule(
        ScraperRuleEditorViewModel rule)
    {
        Rules.Remove(rule);
    }

    private async Task SaveProfileAsync()
    {
        IsBusy = true;

        try
        {
            var profile = BuildProfile();

            if (_builtInProfiles.Any(item =>
                    string.Equals(
                        item.Name,
                        profile.Name,
                        StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    "Built-in profiles are protected. Clone it and use a different name.");
            }

            var existingIndex = _customProfiles.FindIndex(item =>
                string.Equals(
                    item.Name,
                    profile.Name,
                    StringComparison.OrdinalIgnoreCase));

            if (existingIndex >= 0)
            {
                _customProfiles[existingIndex] = profile;
            }
            else
            {
                _customProfiles.Add(profile);
            }

            await _profileStore.SaveAsync(_customProfiles);

            RefreshProfiles(profile.Name);
            StatusMessage =
                $"Saved custom profile '{profile.Name}'.";
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteProfileAsync()
    {
        var selected = SelectedProfile;

        if (selected is null || selected.IsBuiltIn)
        {
            StatusMessage =
                "Built-in profiles cannot be deleted.";
            return;
        }

        IsBusy = true;

        try
        {
            _customProfiles.RemoveAll(item =>
                string.Equals(
                    item.Name,
                    selected.Profile.Name,
                    StringComparison.OrdinalIgnoreCase));

            await _profileStore.SaveAsync(_customProfiles);

            RefreshProfiles();
            SelectedProfile = Profiles.FirstOrDefault();
            StatusMessage =
                $"Deleted custom profile '{selected.Profile.Name}'.";
        }
        catch (ScraperProfilePersistenceException exception)
        {
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task TestProfileAsync()
    {
        IsBusy = true;
        PreviewText = "Downloading and extracting preview…";

        try
        {
            var profile = BuildProfile();
            var result = await _lookupService.LookupAsync(
                profile,
                TestWord);

            var preview = new StringBuilder();
            preview.AppendLine(result.SourceName);
            preview.AppendLine(result.SourceUri.ToString());

            foreach (var field in result.Fields)
            {
                preview.AppendLine();
                preview.AppendLine(field.Key);

                foreach (var value in field.Value)
                {
                    preview.Append("• ");
                    preview.AppendLine(value);
                }
            }

            if (result.Fields.Count == 0)
            {
                preview.AppendLine();
                preview.AppendLine(
                    "No configured fields matched the page.");
            }

            PreviewText = preview.ToString().Trim();
            StatusMessage =
                $"Preview extracted {result.Fields.Count} fields.";
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            PreviewText = exception.Message;
            StatusMessage = "Profile test failed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private ScraperProfile BuildProfile()
    {
        if (string.IsNullOrWhiteSpace(TestWord))
        {
            TestWord = "vielleicht";
        }

        var suggestionFallbackSelectors =
            SuggestionFallbackSelectorsText
                .Split(
                    ['\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries
                        | StringSplitOptions.TrimEntries);
        var suggestionRule = UseClosestSuggestion
            ? new ScraperSuggestionRule(
                SuggestionSelector,
                suggestionFallbackSelectors)
            : null;

        return new ScraperProfile(
            Name,
            SearchUrlTemplate,
            SourceLanguageCode,
            TargetLanguageCode,
            Rules.Select(rule => rule.BuildRule()),
            EntrySelector,
            suggestionRule);
    }

    private void RefreshProfiles(string? selectedName = null)
    {
        Profiles.Clear();

        foreach (var profile in _builtInProfiles)
        {
            Profiles.Add(new ScraperProfileListItem(
                profile,
                IsBuiltIn: true));
        }

        foreach (var profile in _customProfiles
            .OrderBy(item => item.Name))
        {
            Profiles.Add(new ScraperProfileListItem(
                profile,
                IsBuiltIn: false));
        }

        if (selectedName is not null)
        {
            SelectedProfile = Profiles.FirstOrDefault(item =>
                string.Equals(
                    item.Profile.Name,
                    selectedName,
                    StringComparison.OrdinalIgnoreCase));
        }
    }

    private void LoadEditor(ScraperProfile profile)
    {
        Name = profile.Name;
        SearchUrlTemplate = profile.SearchUrlTemplate;
        SourceLanguageCode = profile.SourceLanguageCode;
        TargetLanguageCode = profile.TargetLanguageCode;
        EntrySelector = profile.EntrySelector ?? string.Empty;
        UseClosestSuggestion =
            profile.SuggestionRule is not null;
        SuggestionSelector =
            profile.SuggestionRule?.Selector
                ?? string.Empty;
        SuggestionFallbackSelectorsText =
            string.Join(
                Environment.NewLine,
                profile.SuggestionRule?.FallbackSelectors
                    ?? []);

        Rules.Clear();

        foreach (var rule in profile.Fields)
        {
            Rules.Add(new ScraperRuleEditorViewModel(
                rule,
                RemoveRule));
        }

        PreviewText =
            "Use Test profile to preview extracted values.";
    }

    private string CreateUniqueCustomName(string baseName)
    {
        var candidate = baseName;
        var suffix = 2;

        while (_customProfiles.Any(item =>
                   string.Equals(
                       item.Name,
                       candidate,
                       StringComparison.OrdinalIgnoreCase))
               || _builtInProfiles.Any(item =>
                   string.Equals(
                       item.Name,
                       candidate,
                       StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{baseName} {suffix++}";
        }

        return candidate;
    }

    partial void OnSelectedProfileChanged(
        ScraperProfileListItem? value)
    {
        if (value is null)
        {
            return;
        }

        IsSelectedBuiltIn = value.IsBuiltIn;
        LoadEditor(value.Profile);
        StatusMessage = value.IsBuiltIn
            ? "Built-in profile. Clone it to customize."
            : "Custom profile ready to edit.";
    }
}
